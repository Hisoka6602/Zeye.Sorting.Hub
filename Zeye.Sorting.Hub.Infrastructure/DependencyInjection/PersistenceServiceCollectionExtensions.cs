using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.DependencyInjection {

    /// <summary>
    /// 持久化模块注册扩展（仅负责能力注册，不负责进程启动编排）
    /// </summary>
    public static class PersistenceServiceCollectionExtensions {

        /// <summary>
        /// 针对“无天然时间字段/时间字段可为空”的属性表，采用固定哈希分表。
        /// </summary>
        /// <remarks>
        /// 说明：
        /// 1) 这些表若直接按可空时间分表，插入时可能出现路由不稳定或无法命中分表；
        /// 2) 采用哈希分表可以确保“具备分表能力”，并在数据持续增长时横向分散压力；
        /// 3) 该常量仅作为默认值，实际模数可通过配置项 Persistence:Sharding:ParcelRelatedHashShardingMod 覆盖。
        /// </remarks>
        private const int DefaultParcelRelatedHashShardingMod = 16;

        /// <summary>
        /// Parcel 关联属性表使用的影子外键字段名。
        /// </summary>
        /// <remarks>
        /// 这些值对象表通过 `WithOwner().HasForeignKey("ParcelId")` 建模，
        /// `ParcelId` 属于 EF Core 影子属性，不在 CLR 类型上声明，
        /// 因此这里集中定义常量，避免魔法字符串散落。
        /// </remarks>
        private const string ParcelIdShadowField = "ParcelId";

        /// <summary>
        /// 检测配置字符串结尾是否携带时区后缀（Z、+08:00、-0500 等）。
        /// </summary>
        /// <remarks>
        /// 通过“结尾匹配”避免误伤日期中的连字符，仅拦截真正的时区信息。
        /// </remarks>
        private static readonly Regex TimeZoneSuffixRegex = new(
            pattern: @"(Z|[+\-]\d{2}:\d{2}|[+\-]\d{4})$",
            options: RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// Parcel 关联值对象分表规则：以声明式清单注册，避免注册点继续膨胀为手工长列表。
        /// </summary>
        private static readonly IReadOnlyList<ParcelAggregateShardingRule> ParcelAggregateShardingRules = [
            CreateHashShardingRule<BagInfo>(nameof(BagInfo.BagCode)),
            CreateDateShardingRule<VolumeInfo>(nameof(VolumeInfo.MeasurementTime)),
            CreateDateShardingRule<ChuteInfo>(nameof(ChuteInfo.LandedTime)),
            CreateDateShardingRule<SorterCarrierInfo>(nameof(SorterCarrierInfo.LoadedTime)),
            CreateDateShardingRule<GrayDetectorInfo>(nameof(GrayDetectorInfo.ResultTime)),
            CreateHashShardingRule<ParcelDeviceInfo>(ParcelIdShadowField),
            CreateHashShardingRule<ParcelPositionInfo>(ParcelIdShadowField),
            CreateHashShardingRule<StickingParcelInfo>(ParcelIdShadowField),
            CreateDateShardingRule<ApiRequestInfo>(nameof(ApiRequestInfo.RequestTime)),
            CreateDateShardingRule<CommandInfo>(nameof(CommandInfo.GeneratedTime)),
            CreateDateShardingRule<WeightInfo>(nameof(WeightInfo.WeighingTime)),
            CreateHashShardingRule<BarCodeInfo>(ParcelIdShadowField),
            CreateHashShardingRule<ImageInfo>(ParcelIdShadowField),
            CreateHashShardingRule<VideoInfo>(ParcelIdShadowField)
        ];

        /// <summary>
        /// 注册持久化层核心能力（EF Core、分表规则、自动调优拦截器与观测组件），并按 <c>Persistence:Provider</c> 选择数据库方言实现。
        /// </summary>
        public static IServiceCollection AddSortingHubPersistence(this IServiceCollection services, IConfiguration configuration) {
            var provider = configuration["Persistence:Provider"];
            var commandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var minCommandElapsedMilliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MinCommandElapsedMilliseconds", 50);
            var parcelShardingStartTime = GetShardingStartTime(configuration);
            var createShardingTableOnStarting = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, "Persistence:Sharding:CreateShardingTableOnStarting", false);
            var parcelRelatedHashShardingMod = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, "Persistence:Sharding:ParcelRelatedHashShardingMod", DefaultParcelRelatedHashShardingMod);
            var parcelShardingStrategyEvaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
            var parcelShardingStrategyDecision = parcelShardingStrategyEvaluation.Decision;
            if (parcelShardingStrategyEvaluation.ValidationErrors.Count > 0) {
                throw new InvalidOperationException(
                    $"分表策略配置校验失败：{string.Join(" | ", parcelShardingStrategyEvaluation.ValidationErrors)}");
            }

            services.AddSingleton<SlowQueryAutoTuningPipeline>();
            services.AddSingleton<SlowQueryCommandInterceptor>();
            services.AddSingleton<MySqlSessionBootstrapConnectionInterceptor>();
            services.TryAddSingleton<IAutoTuningObservability, NullAutoTuningObservability>();
            services.TryAddSingleton<IExecutionPlanRegressionProbe, LoggingOnlyExecutionPlanRegressionProbe>();

            if (string.IsNullOrWhiteSpace(provider)) {
                throw new InvalidOperationException("缺少配置：Persistence:Provider，可选值：MySql / SqlServer");
            }

            if (string.Equals(provider, "MySql", StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString("MySql");
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException("缺少连接字符串：ConnectionStrings:MySql");
                }

                // DbContextPool：更低分配、更稳吞吐
                services.AddDbContextPool<SortingHubDbContext>(static (sp, options) => {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var interceptor = sp.GetRequiredService<SlowQueryCommandInterceptor>();
                    var mySqlSessionInterceptor = sp.GetRequiredService<MySqlSessionBootstrapConnectionInterceptor>();
                    var cs = cfg.GetConnectionString("MySql")!;
                    var commandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
                    var maxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
                    var maxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

                    // 建议：生产环境可改为固定版本，避免探测失败导致启动失败
                    var serverVersion = ServerVersion.AutoDetect(cs);

                    options.UseMySql(cs, serverVersion, mySqlOptions => {
                        // 迁移程序集通常指向 Host 或 Infrastructure，按你迁移放置位置调整
                        // mySqlOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                        mySqlOptions.EnableRetryOnFailure(
                            maxRetryCount: maxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                            errorNumbersToAdd: null);
                        mySqlOptions.CommandTimeout(commandTimeoutSeconds);
                    });

                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                    options.AddInterceptors(interceptor, mySqlSessionInterceptor);

                    // 开发环境建议开启，生产建议关闭
                    // options.EnableSensitiveDataLogging();
                    // options.EnableDetailedErrors();
                });

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(createShardingTableOnStarting)
                        .UseDatabase(connectionString, DatabaseType.MySql, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime, parcelRelatedHashShardingMod, parcelShardingStrategyDecision);
                });

                services.AddSingleton<IDatabaseDialect, MySqlDialect>();
                services.AddSingleton<IShardingPhysicalTableProbe>(sp =>
                    (IShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
            }
            else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString("SqlServer");
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException("缺少连接字符串：ConnectionStrings:SqlServer");
                }

                services.AddDbContextPool<SortingHubDbContext>(static (sp, options) => {
                    var cfg = sp.GetRequiredService<IConfiguration>();
                    var interceptor = sp.GetRequiredService<SlowQueryCommandInterceptor>();
                    var cs = cfg.GetConnectionString("SqlServer")!;
                    var commandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
                    var maxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
                    var maxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

                    options.UseSqlServer(cs, sqlServerOptions => {
                        // 迁移程序集通常指向 Host 或 Infrastructure，按你迁移放置位置调整
                        // sqlServerOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                        sqlServerOptions.EnableRetryOnFailure(
                            maxRetryCount: maxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                            errorNumbersToAdd: null);
                        sqlServerOptions.CommandTimeout(commandTimeoutSeconds);
                    });

                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                    options.AddInterceptors(interceptor);
                });

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(createShardingTableOnStarting)
                        .UseDatabase(connectionString, DatabaseType.SqlServer, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime, parcelRelatedHashShardingMod, parcelShardingStrategyDecision);
                });

                services.AddSingleton<IDatabaseDialect, SqlServerDialect>();
                services.AddSingleton<IShardingPhysicalTableProbe>(sp =>
                    (IShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
            }
            else {
                throw new InvalidOperationException($"不支持的数据库类型：{provider}，可选值：MySql / SqlServer");
            }

            // 仓储注册（示例）：按你现有 RepositoryBase/MemoryCacheRepositoryBase 的实际泛型/接口进行补齐
            // services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

            return services;
        }

        /// <summary>
        /// 获取 Parcel 体系按日分表治理需要关注的实体类型清单（与分表注册规则同源）。
        /// </summary>
        /// <returns>实体类型清单。</returns>
        public static IReadOnlyList<Type> GetParcelPerDayShardingEntityTypes() {
            return ParcelAggregateShardingRules
                .Where(static rule => rule.RuleKind == ParcelAggregateShardingRuleKind.Date)
                .Select(static rule => rule.EntityType)
                .Prepend(typeof(Parcel))
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// 统一注册 Parcel 主表与属性表的分表规则。
        /// </summary>
        /// <param name="shardingBuilder">分表构建器。</param>
        /// <param name="parcelShardingStartTime">Parcel 月分表起始时间。</param>
        /// <param name="parcelRelatedHashShardingMod">Parcel 关联属性表哈希分片模数。</param>
        /// <param name="parcelShardingStrategyDecision">Parcel 分表策略决策快照。</param>
        /// <remarks>
        /// 设计目标：
        /// - Parcel 主表继续基于 CreatedTime 做时间路由；
        /// - 与 Parcel 同步增长的属性表也必须具备分表能力，避免单表无限膨胀；
        /// - 对“有稳定必填时间字段”的表按策略决策采用按月/按天分表；
        /// - 对“无时间字段/时间可空”的表采用哈希分表，保证可路由且可扩展。
        /// </remarks>
        private static void ConfigureParcelAggregateSharding(
            IShardingBuilder shardingBuilder,
            DateTime parcelShardingStartTime,
            int parcelRelatedHashShardingMod,
            ParcelShardingStrategyDecision parcelShardingStrategyDecision) {
            AssertParcelAggregateShardingCoverage();

            // 分表起始时间在进入规则注册前统一归一化为“本地时间语义”，
            // 避免外部配置传入未指定 Kind 的时间值时产生路由歧义。
            var localShardingStartTime = AutoTuningConfigurationHelper.NormalizeToLocalTime(parcelShardingStartTime);

            // ------------------------------
            // 1) 主表：Parcel（CreatedTime + 策略决策粒度）
            // ------------------------------
            shardingBuilder.SetDateSharding<Parcel>(
                shardingField: nameof(Parcel.CreatedTime),
                expandByDateMode: parcelShardingStrategyDecision.EffectiveDateMode,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 2) Parcel 关联值对象：按规则清单统一注册
            // ------------------------------
            foreach (var rule in ParcelAggregateShardingRules) {
                rule.Register(shardingBuilder, localShardingStartTime, parcelRelatedHashShardingMod, parcelShardingStrategyDecision.EffectiveDateMode);
            }
        }

        /// <summary>
        /// 启动期分表覆盖守卫：校验 Parcel 值对象候选类型是否均已配置分表规则。
        /// </summary>
        /// <remarks>
        /// 目的：当新增 ValueObjects 下的 *Info 类型时，若遗漏分表注册，启动期立即失败并给出缺失清单，
        /// 避免运行期出现“路由命中但物理分表规则缺失”的异常。
        /// </remarks>
        internal static void AssertParcelAggregateShardingCoverage() {
            var configured = ParcelAggregateShardingRules
                .Select(static rule => rule.EntityType)
                .ToHashSet();
            var discoveredCandidates = DiscoverParcelAggregateShardingCandidates();
            var missing = discoveredCandidates
                .Where(type => !configured.Contains(type))
                .OrderBy(static type => type.Name, StringComparer.Ordinal)
                .Select(static type => type.Name)
                .ToArray();
            if (missing.Length == 0) {
                return;
            }

            throw new InvalidOperationException(
                $"检测到 Parcel 值对象分表规则缺失：{string.Join(", ", missing)}。请在 ConfigureParcelAggregateSharding 中同步补充分表规则，并确认 EF Core 迁移与分表治理配置已对齐。");
        }

        /// <summary>发现 Parcel 值对象目录下需要分表治理的候选类型。</summary>
        private static IReadOnlyList<Type> DiscoverParcelAggregateShardingCandidates() {
            var valueObjectNamespace = typeof(BagInfo).Namespace;
            return typeof(Parcel).Assembly
                .GetTypes()
                .Where(type =>
                    type.IsClass
                    && !type.IsAbstract
                    && type.IsPublic
                    && string.Equals(type.Namespace, valueObjectNamespace, StringComparison.Ordinal)
                    && type.Name.EndsWith("Info", StringComparison.Ordinal))
                .ToArray();
        }

        /// <summary>
        /// 创建“按时间粒度分表”规则描述。
        /// </summary>
        /// <typeparam name="TEntity">实体类型。</typeparam>
        /// <param name="shardingField">分片字段名。</param>
        /// <returns>规则描述对象。</returns>
        private static ParcelAggregateShardingRule CreateDateShardingRule<TEntity>(string shardingField)
            where TEntity : class {
            return new ParcelAggregateShardingRule(
                EntityType: typeof(TEntity),
                RuleKind: ParcelAggregateShardingRuleKind.Date,
                Register: (builder, startTime, _, dateMode) => builder.SetDateSharding<TEntity>(
                    shardingField: shardingField,
                    expandByDateMode: dateMode,
                    startTime: startTime,
                    sourceName: ShardingConstant.DefaultSource));
        }

        /// <summary>
        /// 创建“哈希分表”规则描述。
        /// </summary>
        /// <typeparam name="TEntity">实体类型。</typeparam>
        /// <param name="shardingField">分片字段名。</param>
        /// <returns>规则描述对象。</returns>
        private static ParcelAggregateShardingRule CreateHashShardingRule<TEntity>(string shardingField)
            where TEntity : class {
            return new ParcelAggregateShardingRule(
                EntityType: typeof(TEntity),
                RuleKind: ParcelAggregateShardingRuleKind.Hash,
                Register: (builder, _, mod, _) => builder.SetHashModSharding<TEntity>(
                    shardingField: shardingField,
                    mod: mod,
                    sourceName: ShardingConstant.DefaultSource));
        }

        /// <summary>
        /// 执行逻辑：GetShardingStartTime。
        /// </summary>
        private static DateTime GetShardingStartTime(IConfiguration configuration) {
            var configured = configuration["Persistence:Sharding:ParcelStartTime"];

            // 严格禁止配置使用 Z / offset（如 +08:00 / -0500）语义。
            // 该校验必须发生在 DateTime.TryParse 之前，避免被解析器自动折算后绕过。
            EnsureNoTimeZoneSuffix(configured);

            if (DateTime.TryParse(
                configured,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed)) {
                // 统一复用本地时间归一化入口，避免规则分叉。
                return AutoTuningConfigurationHelper.NormalizeToLocalTime(parsed);
            }

            var now = DateTime.Now;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);
        }

        /// <summary>
        /// 校验分表起始时间配置中不允许出现时区后缀。
        /// </summary>
        /// <param name="configured">原始配置值。</param>
        /// <exception cref="InvalidOperationException">当配置包含 Z/offset 时抛出。</exception>
        private static void EnsureNoTimeZoneSuffix(string? configured) {
            if (string.IsNullOrWhiteSpace(configured)) {
                return;
            }

            var configuredText = configured.Trim();
            if (TimeZoneSuffixRegex.IsMatch(configuredText)) {
                var safeConfiguredText = GetSafeConfigPreview(configuredText);
                throw new InvalidOperationException($"配置项 Persistence:Sharding:ParcelStartTime 仅支持本地时间语义，禁止使用 Z 或 offset。检测到：{safeConfiguredText}");
            }
        }

        /// <summary>
        /// 生成适合放入异常消息的安全配置预览文本。
        /// </summary>
        /// <param name="configuredText">原始配置文本。</param>
        /// <returns>移除控制字符并截断后的预览文本。</returns>
        private static string GetSafeConfigPreview(string configuredText) {
            var builder = new StringBuilder(configuredText.Length);
            foreach (var c in configuredText) {
                if (!char.IsControl(c)) {
                    builder.Append(c);
                }
            }

            var sanitized = builder.ToString();
            const int maxLength = 120;
            return sanitized.Length <= maxLength ? sanitized : $"{sanitized[..maxLength]}...";
        }

        /// <summary>
        /// Parcel 聚合分表规则描述（统一声明式定义）。
        /// </summary>
        /// <param name="EntityType">规则对应实体类型。</param>
        /// <param name="Register">规则注册动作。</param>
        private readonly record struct ParcelAggregateShardingRule(
            Type EntityType,
            ParcelAggregateShardingRuleKind RuleKind,
            Action<IShardingBuilder, DateTime, int, ExpandByDateMode> Register);
    }
}
