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
        /// 3) 取模值保持温和，兼顾分散效果与运维复杂度。
        /// </remarks>
        private const int ParcelRelatedHashShardingMod = 16;

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

        public static IServiceCollection AddSortingHubPersistence(this IServiceCollection services, IConfiguration configuration) {
            var provider = configuration["Persistence:Provider"];
            var commandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var minCommandElapsedMilliseconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MinCommandElapsedMilliseconds", 50);
            var parcelShardingStartTime = GetShardingStartTime(configuration);

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
                    var commandTimeoutSeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
                    var maxRetryCount = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
                    var maxRetryDelaySeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

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
                        .CreateShardingTableOnStarting(false)
                        .UseDatabase(connectionString, DatabaseType.MySql, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime);
                });

                services.AddSingleton<IDatabaseDialect, MySqlDialect>();
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
                    var commandTimeoutSeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
                    var maxRetryCount = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
                    var maxRetryDelaySeconds = GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

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
                        .CreateShardingTableOnStarting(false)
                        .UseDatabase(connectionString, DatabaseType.SqlServer, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime);
                });

                services.AddSingleton<IDatabaseDialect, SqlServerDialect>();
            }
            else {
                throw new InvalidOperationException($"不支持的数据库类型：{provider}，可选值：MySql / SqlServer");
            }

            // 仓储注册（示例）：按你现有 RepositoryBase/MemoryCacheRepositoryBase 的实际泛型/接口进行补齐
            // services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));

            return services;
        }

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) =>
            AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, key, fallback);

        /// <summary>
        /// 统一注册 Parcel 主表与属性表的分表规则。
        /// </summary>
        /// <param name="shardingBuilder">分表构建器。</param>
        /// <param name="parcelShardingStartTime">Parcel 月分表起始时间。</param>
        /// <remarks>
        /// 设计目标：
        /// - Parcel 主表已经采用按月分表；
        /// - 与 Parcel 同步增长的属性表也必须具备分表能力，避免单表无限膨胀；
        /// - 对“有稳定必填时间字段”的表优先采用按月分表；
        /// - 对“无时间字段/时间可空”的表采用哈希分表，保证可路由且可扩展。
        /// </remarks>
        private static void ConfigureParcelAggregateSharding(IShardingBuilder shardingBuilder, DateTime parcelShardingStartTime) {
            // 分表起始时间在进入规则注册前统一归一化为“本地时间语义”，
            // 避免外部配置传入未指定 Kind 的时间值时产生路由歧义。
            var localShardingStartTime = NormalizeToLocalTime(parcelShardingStartTime);

            // ------------------------------
            // 1) 主表：Parcel（按月分表）
            // ------------------------------
            shardingBuilder.SetDateSharding<Parcel>(
                shardingField: nameof(Parcel.CreatedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 2) 独立属性实体：BagInfo
            // ------------------------------
            // BagInfo 的 BaggingTime 可为空，采用 BagCode 哈希分表以保证路由稳定。
            shardingBuilder.SetHashModSharding<BagInfo>(
                shardingField: nameof(BagInfo.BagCode),
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 3) 一对一属性表（OwnsOne）
            // ------------------------------
            // 具备必填时间字段 -> 按月分表
            shardingBuilder.SetDateSharding<VolumeInfo>(
                shardingField: nameof(VolumeInfo.MeasurementTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<ChuteInfo>(
                shardingField: nameof(ChuteInfo.LandedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<SorterCarrierInfo>(
                shardingField: nameof(SorterCarrierInfo.LoadedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<GrayDetectorInfo>(
                shardingField: nameof(GrayDetectorInfo.ResultTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // 无稳定时间字段或时间可空 -> 按 ParcelId 哈希分表
            shardingBuilder.SetHashModSharding<ParcelDeviceInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<ParcelPositionInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<StickingParcelInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);

            // ------------------------------
            // 4) 一对多属性表（OwnsMany）
            // ------------------------------
            // 具备必填时间字段 -> 按月分表
            shardingBuilder.SetDateSharding<ApiRequestInfo>(
                shardingField: nameof(ApiRequestInfo.RequestTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<CommandInfo>(
                shardingField: nameof(CommandInfo.GeneratedTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<WeightInfo>(
                shardingField: nameof(WeightInfo.WeighingTime),
                expandByDateMode: ExpandByDateMode.PerMonth,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);

            // 可空时间或无时间字段 -> 按 ParcelId 哈希分表
            shardingBuilder.SetHashModSharding<BarCodeInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<ImageInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetHashModSharding<VideoInfo>(
                shardingField: ParcelIdShadowField,
                mod: ParcelRelatedHashShardingMod,
                sourceName: ShardingConstant.DefaultSource);
        }

        /// <summary>
        /// 将任意 <see cref="DateTime"/> 统一转换为本地时间语义。
        /// </summary>
        /// <param name="value">待归一化的时间值。</param>
        /// <returns>带有 <see cref="DateTimeKind.Local"/> 语义的时间值。</returns>
        /// <remarks>
        /// 项目约束要求统一使用本地时间语义：
        /// - Unspecified：按“本地时间”解释并补齐 Kind；
        /// - Local：原样返回；
        /// - 其他：视为不合法输入并抛错（禁止 UTC/带 offset 的时间语义进入链路）。
        /// </remarks>
        private static DateTime NormalizeToLocalTime(DateTime value) {
            return value.Kind switch {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
                DateTimeKind.Local => value,
                _ => throw new InvalidOperationException("仅支持本地时间语义，请勿传入 UTC 或带 offset 的时间值。")
            };
        }

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
                return NormalizeToLocalTime(parsed);
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
    }
}
