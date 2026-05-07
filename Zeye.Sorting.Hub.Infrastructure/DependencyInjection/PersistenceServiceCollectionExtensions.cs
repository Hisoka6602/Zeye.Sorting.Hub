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
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Infrastructure.Repositories;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

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
        /// Parcel 关联属性表使用的外键字段名。
        /// </summary>
        /// <remarks>
        /// 这些值对象表通过 `WithOwner().HasForeignKey("ParcelId")` 建模，
        /// 并在值对象 CLR 类型上显式声明 `ParcelId` 属性用于分片字段识别；
        /// 因此这里集中定义常量，避免魔法字符串散落。
        /// </remarks>
        private const string ParcelIdField = "ParcelId";

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
        /// NLog 日志器。
        /// </summary>
        private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Parcel 关联值对象分表规则：以声明式清单注册，避免注册点继续膨胀为手工长列表。
        /// </summary>
        private static readonly IReadOnlyList<ParcelAggregateShardingRule> ParcelAggregateShardingRules = [
            CreateHashShardingRule<BagInfo>(nameof(BagInfo.BagCode)),
            CreateDateShardingRule<VolumeInfo>(nameof(VolumeInfo.MeasurementTime)),
            CreateDateShardingRule<ChuteInfo>(nameof(ChuteInfo.LandedTime)),
            CreateDateShardingRule<SorterCarrierInfo>(nameof(SorterCarrierInfo.LoadedTime)),
            CreateDateShardingRule<GrayDetectorInfo>(nameof(GrayDetectorInfo.ResultTime)),
            CreateHashShardingRule<ParcelDeviceInfo>(ParcelIdField),
            CreateHashShardingRule<ParcelPositionInfo>(ParcelIdField),
            CreateHashShardingRule<StickingParcelInfo>(ParcelIdField),
            CreateDateShardingRule<ApiRequestInfo>(nameof(ApiRequestInfo.RequestTime)),
            CreateDateShardingRule<CommandInfo>(nameof(CommandInfo.GeneratedTime)),
            CreateDateShardingRule<WeightInfo>(nameof(WeightInfo.WeighingTime)),
            CreateHashShardingRule<BarCodeInfo>(ParcelIdField),
            CreateHashShardingRule<ImageInfo>(ParcelIdField),
            CreateHashShardingRule<VideoInfo>(ParcelIdField)
        ];
        /// <summary>
        /// WebRequestAuditLog 冷热模型分表规则：StartedAt 统一按日分表（与治理探测同源）。
        /// </summary>
        private static readonly IReadOnlyList<(Type EntityType, string ShardingField)> WebRequestAuditLogPerDayShardingRules = [
            (typeof(WebRequestAuditLog), nameof(WebRequestAuditLog.StartedAt)),
            (typeof(WebRequestAuditLogDetail), nameof(WebRequestAuditLogDetail.StartedAt))
        ];

        /// <summary>
        /// 注册持久化层核心能力（EF Core、分表规则、自动调优拦截器与观测组件），并按 <c>Persistence:Provider</c> 选择数据库方言实现。
        /// 配置值与 ConnectionStrings key 语义由 <see cref="ConfiguredProviderNames"/> 统一定义；
        /// EF Core 运行时 providerName 语义由 <see cref="DbProviderNames"/> 统一定义。
        /// </summary>
        public static IServiceCollection AddSortingHubPersistence(this IServiceCollection services, IConfiguration configuration) {
            RegisterDatabaseConnectionDiagnostics(services, configuration);
            RegisterBufferedWriteServices(services, configuration);
            RegisterShardingGovernanceServices(services, configuration);
            RegisterDataArchiveServices(services, configuration);
            RegisterBackupGovernanceServices(services, configuration);
            RegisterDataRetentionServices(services, configuration);
            RegisterBaselineDataServices(services, configuration);
            RegisterMigrationGovernanceServices(services);
            var provider = configuration["Persistence:Provider"];
            var commandTimeoutSeconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var minCommandElapsedMilliseconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MinCommandElapsedMilliseconds", 50);
            var parcelShardingStartTime = GetShardingStartTime(configuration);
            var createShardingTableOnStarting = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:CreateShardingTableOnStarting", false);
            var parcelRelatedHashShardingMod = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, "Persistence:Sharding:ParcelRelatedHashShardingMod", DefaultParcelRelatedHashShardingMod);
            var parcelShardingStrategyEvaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
            var parcelShardingStrategyDecision = parcelShardingStrategyEvaluation.Decision;
            if (parcelShardingStrategyEvaluation.ValidationErrors.Count > 0) {
                throw new InvalidOperationException(
                    $"分表策略配置校验失败：{string.Join(" | ", parcelShardingStrategyEvaluation.ValidationErrors)}");
            }

            services.AddSingleton<SlowQueryAutoTuningPipeline>();
            services.AddSingleton<SlowQueryProfileStore>();
            services.AddSingleton<Zeye.Sorting.Hub.Application.Abstractions.Diagnostics.ISlowQueryProfileReader>(static serviceProvider =>
                serviceProvider.GetRequiredService<SlowQueryProfileStore>());
            services.TryAddSingleton<QueryTemplateRegistry>();
            services.TryAddSingleton<QueryIndexRecommendationService>();
            services.TryAddSingleton<IdempotencyKeyHasher>();
            services.AddSingleton<SlowQueryCommandInterceptor>();
            services.AddSingleton<MySqlSessionBootstrapConnectionInterceptor>();
            services.TryAddSingleton<IAutoTuningObservability, NullAutoTuningObservability>();
            services.TryAddSingleton<IExecutionPlanRegressionProbe, LoggingOnlyExecutionPlanRegressionProbe>();

            if (string.IsNullOrWhiteSpace(provider)) {
                throw new InvalidOperationException($"缺少配置：Persistence:Provider，可选值：{ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}");
            }

            if (string.Equals(provider, ConfiguredProviderNames.MySql, StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString(ConfiguredProviderNames.MySql);
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException($"缺少连接字符串：ConnectionStrings:{ConfiguredProviderNames.MySql}");
                }

                // DbContextPool：更低分配、更稳吞吐
                // AddDbContextPool：兼容现有直接注入 SortingHubDbContext 的路径（如 HostedService）。
                // AddPooledDbContextFactory：供仓储基类通过 IDbContextFactory 按调用创建短生命周期上下文。
                services.AddDbContextPool<SortingHubDbContext>(ConfigureMySqlDbContextOptions);
                services.AddPooledDbContextFactory<SortingHubDbContext>(ConfigureMySqlDbContextOptions);

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(createShardingTableOnStarting)
                        .UseDatabase(connectionString, DatabaseType.MySql, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime, parcelRelatedHashShardingMod, parcelShardingStrategyDecision);
                    ConfigureWebRequestAuditLogSharding(shardingBuilder, parcelShardingStartTime);
                });

                services.AddSingleton<IDatabaseDialect, MySqlDialect>();
                services.TryAddSingleton<IBackupProvider, MySqlBackupProvider>();
                services.AddSingleton<IShardingPhysicalTableProbe>(sp =>
                    (IShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
                services.AddSingleton<IBatchShardingPhysicalTableProbe>(sp =>
                    (IBatchShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
            }
            else if (string.Equals(provider, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
                var connectionString = configuration.GetConnectionString(ConfiguredProviderNames.SqlServer);
                if (string.IsNullOrWhiteSpace(connectionString)) {
                    throw new InvalidOperationException($"缺少连接字符串：ConnectionStrings:{ConfiguredProviderNames.SqlServer}");
                }

                // AddDbContextPool：兼容现有直接注入 SortingHubDbContext 的路径（如 HostedService）。
                // AddPooledDbContextFactory：供仓储基类通过 IDbContextFactory 按调用创建短生命周期上下文。
                services.AddDbContextPool<SortingHubDbContext>(ConfigureSqlServerDbContextOptions);
                services.AddPooledDbContextFactory<SortingHubDbContext>(ConfigureSqlServerDbContextOptions);

                services.AddEFCoreSharding(shardingBuilder => {
                    shardingBuilder
                        .SetEntityAssemblies(typeof(SortingHubDbContext).Assembly)
                        .SetCommandTimeout(commandTimeoutSeconds)
                        .SetMinCommandElapsedMilliseconds(minCommandElapsedMilliseconds)
                        .CreateShardingTableOnStarting(createShardingTableOnStarting)
                        .UseDatabase(connectionString, DatabaseType.SqlServer, typeof(Parcel).Namespace!, static _ => { });

                    ConfigureParcelAggregateSharding(shardingBuilder, parcelShardingStartTime, parcelRelatedHashShardingMod, parcelShardingStrategyDecision);
                    ConfigureWebRequestAuditLogSharding(shardingBuilder, parcelShardingStartTime);
                });

                services.AddSingleton<IDatabaseDialect, SqlServerDialect>();
                services.TryAddSingleton<IBackupProvider, SqlServerBackupProvider>();
                services.AddSingleton<IShardingPhysicalTableProbe>(sp =>
                    (IShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
                services.AddSingleton<IBatchShardingPhysicalTableProbe>(sp =>
                    (IBatchShardingPhysicalTableProbe)sp.GetRequiredService<IDatabaseDialect>());
            }
            else {
                throw new InvalidOperationException($"不支持的数据库类型：{provider}，可选值：{ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}");
            }

            services.AddScoped<IParcelRepository, ParcelRepository>();
            services.AddScoped<IArchiveTaskRepository, ArchiveTaskRepository>();
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            services.AddScoped<IInboxMessageRepository, InboxMessageRepository>();
            services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
            services.AddScoped<WebRequestAuditLogRepository>();
            services.AddScoped<IWebRequestAuditLogRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<WebRequestAuditLogRepository>());
            services.AddScoped<IWebRequestAuditLogQueryRepository>(serviceProvider =>
                serviceProvider.GetRequiredService<WebRequestAuditLogRepository>());

            return services;
        }

        /// <summary>
        /// 注册数据库连接诊断与预热能力。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterDatabaseConnectionDiagnostics(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<DatabaseConnectionDiagnosticsOptions>()
                .Configure(options => ApplyDatabaseConnectionDiagnosticsOptions(options, configuration))
                .Validate(
                    static options => options.WarmupConnectionCount is >= DatabaseConnectionDiagnosticsOptions.MinWarmupConnectionCount and <= DatabaseConnectionDiagnosticsOptions.MaxWarmupConnectionCount,
                    $"WarmupConnectionCount 必须在 {DatabaseConnectionDiagnosticsOptions.MinWarmupConnectionCount}~{DatabaseConnectionDiagnosticsOptions.MaxWarmupConnectionCount} 之间")
                .Validate(
                    static options => options.ProbeTimeoutMilliseconds is >= DatabaseConnectionDiagnosticsOptions.MinProbeTimeoutMilliseconds and <= DatabaseConnectionDiagnosticsOptions.MaxProbeTimeoutMilliseconds,
                    $"ProbeTimeoutMilliseconds 必须在 {DatabaseConnectionDiagnosticsOptions.MinProbeTimeoutMilliseconds}~{DatabaseConnectionDiagnosticsOptions.MaxProbeTimeoutMilliseconds} 之间")
                .Validate(
                    static options => options.FailureThreshold is >= DatabaseConnectionDiagnosticsOptions.MinFailureThreshold and <= DatabaseConnectionDiagnosticsOptions.MaxFailureThreshold,
                    $"FailureThreshold 必须在 {DatabaseConnectionDiagnosticsOptions.MinFailureThreshold}~{DatabaseConnectionDiagnosticsOptions.MaxFailureThreshold} 之间")
                .Validate(
                    static options => options.RecoveryThreshold is >= DatabaseConnectionDiagnosticsOptions.MinRecoveryThreshold and <= DatabaseConnectionDiagnosticsOptions.MaxRecoveryThreshold,
                    $"RecoveryThreshold 必须在 {DatabaseConnectionDiagnosticsOptions.MinRecoveryThreshold}~{DatabaseConnectionDiagnosticsOptions.MaxRecoveryThreshold} 之间")
                .ValidateOnStart();

            services.TryAddSingleton<IDatabaseConnectionDiagnostics, DatabaseConnectionDiagnosticsService>();
            services.TryAddSingleton<DatabaseConnectionWarmupService>();
        }

        /// <summary>
        /// 注册数据库迁移治理能力。
        /// </summary>
        /// <param name="services">服务集合。</param>
        private static void RegisterMigrationGovernanceServices(IServiceCollection services) {
            services.TryAddSingleton<MigrationSafetyEvaluator>();
            services.TryAddSingleton<MigrationRollbackScriptProvider>();
            services.TryAddSingleton<MigrationScriptArchiveService>();
            services.TryAddSingleton<MigrationGovernanceStateStore>();
        }

        /// <summary>
        /// 注册基线数据校验与种子入口。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterBaselineDataServices(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<BaselineDataOptions>()
                .Configure(options => BaselineDataOptions.Apply(options, configuration))
                .Validate(
                    static options => BaselineDataOptions.IsSupportedFailureMode(options.FailureMode),
                    "Persistence:BaselineData:FailureMode 仅允许填写 Degraded / FailFast")
                .ValidateOnStart();

            services.TryAddSingleton<BaselineDataValidator>();
            services.TryAddSingleton<BaselineDataSeeder>();
        }

        /// <summary>
        /// 注册分表运行期治理能力。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterShardingGovernanceServices(IServiceCollection services, IConfiguration configuration) {
            var parcelShardingStrategyEvaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
            services.AddOptions<ShardingRuntimeInspectionOptions>()
                .Configure(options => ApplyShardingRuntimeInspectionOptions(options, configuration))
                .Validate(
                    static options => options.InspectionIntervalMinutes is >= ShardingRuntimeInspectionOptions.MinInspectionIntervalMinutes and <= ShardingRuntimeInspectionOptions.MaxInspectionIntervalMinutes,
                    $"InspectionIntervalMinutes 必须在 {ShardingRuntimeInspectionOptions.MinInspectionIntervalMinutes}~{ShardingRuntimeInspectionOptions.MaxInspectionIntervalMinutes} 之间")
                .ValidateOnStart();
            services.AddOptions<ShardingPrebuildOptions>()
                .Configure(options => ApplyShardingPrebuildOptions(options, configuration))
                .Validate(
                    static options => options.PrebuildAheadHours is >= ShardingPrebuildOptions.MinPrebuildAheadHours and <= ShardingPrebuildOptions.MaxPrebuildAheadHours,
                    $"PrebuildAheadHours 必须在 {ShardingPrebuildOptions.MinPrebuildAheadHours}~{ShardingPrebuildOptions.MaxPrebuildAheadHours} 之间")
                .Validate(
                    static options => options.DryRun,
                    "当前版本仅允许 Persistence:Sharding:Prebuild:DryRun=true；真实预建需先接入危险动作隔离器")
                .ValidateOnStart();

            services.TryAddSingleton(new ShardingPhysicalTablePlanBuilder(parcelShardingStrategyEvaluation.Decision));
            services.TryAddSingleton<ShardingCapacitySnapshotService>();
            services.TryAddSingleton<ShardingIndexInspectionService>();
            services.TryAddSingleton<ShardingTableInspectionService>();
            services.TryAddSingleton<ShardingTablePrebuildService>();
        }

        /// <summary>
        /// 注册批量缓冲写入服务。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterBufferedWriteServices(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<BufferedWriteOptions>()
                .Configure(options => ApplyBufferedWriteOptions(options, configuration))
                .Validate(
                    static options => options.ChannelCapacity is >= BufferedWriteOptions.MinChannelCapacity and <= BufferedWriteOptions.MaxChannelCapacity,
                    $"ChannelCapacity 必须在 {BufferedWriteOptions.MinChannelCapacity}~{BufferedWriteOptions.MaxChannelCapacity} 之间")
                .Validate(
                    static options => options.BatchSize is >= BufferedWriteOptions.MinBatchSize and <= BufferedWriteOptions.MaxBatchSize,
                    $"BatchSize 必须在 {BufferedWriteOptions.MinBatchSize}~{BufferedWriteOptions.MaxBatchSize} 之间")
                .Validate(
                    static options => options.FlushIntervalMilliseconds is >= BufferedWriteOptions.MinFlushIntervalMilliseconds and <= BufferedWriteOptions.MaxFlushIntervalMilliseconds,
                    $"FlushIntervalMilliseconds 必须在 {BufferedWriteOptions.MinFlushIntervalMilliseconds}~{BufferedWriteOptions.MaxFlushIntervalMilliseconds} 之间")
                .Validate(
                    static options => options.MaxRetryCount is >= BufferedWriteOptions.MinMaxRetryCount and <= BufferedWriteOptions.MaxMaxRetryCount,
                    $"MaxRetryCount 必须在 {BufferedWriteOptions.MinMaxRetryCount}~{BufferedWriteOptions.MaxMaxRetryCount} 之间")
                .Validate(
                    static options => options.DeadLetterCapacity is >= BufferedWriteOptions.MinDeadLetterCapacity and <= BufferedWriteOptions.MaxDeadLetterCapacity,
                    $"DeadLetterCapacity 必须在 {BufferedWriteOptions.MinDeadLetterCapacity}~{BufferedWriteOptions.MaxDeadLetterCapacity} 之间")
                .Validate(
                    static options => options.BackpressureRejectThreshold > 0 && options.BackpressureRejectThreshold <= options.ChannelCapacity,
                    "BackpressureRejectThreshold 必须在 1~ChannelCapacity 之间")
                .Validate(
                    static options => options.BatchSize <= options.ChannelCapacity,
                    "BatchSize 不能大于 ChannelCapacity")
                .ValidateOnStart();

            services.TryAddSingleton(sp =>
                new BoundedWriteChannel<BufferedParcelWriteItem>(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BufferedWriteOptions>>().Value.ChannelCapacity));
            services.TryAddSingleton(sp =>
                new DeadLetterWriteStore(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BufferedWriteOptions>>().Value.DeadLetterCapacity));
            services.TryAddSingleton<ParcelBufferedWriteService>();
            services.TryAddSingleton<ParcelBatchWriteFlushService>();
            services.TryAddSingleton<IBufferedWriteService>(sp => sp.GetRequiredService<ParcelBufferedWriteService>());
        }

        /// <summary>
        /// 注册数据归档 dry-run 服务。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterDataArchiveServices(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<DataArchiveOptions>()
                .Configure(options => ApplyDataArchiveOptions(options, configuration))
                .Validate(
                    static options => options.WorkerPollIntervalSeconds is >= DataArchiveOptions.MinWorkerPollIntervalSeconds and <= DataArchiveOptions.MaxWorkerPollIntervalSeconds,
                    $"WorkerPollIntervalSeconds 必须在 {DataArchiveOptions.MinWorkerPollIntervalSeconds}~{DataArchiveOptions.MaxWorkerPollIntervalSeconds} 之间")
                .Validate(
                    static options => options.SampleItemLimit is >= DataArchiveOptions.MinSampleItemLimit and <= DataArchiveOptions.MaxSampleItemLimit,
                    $"SampleItemLimit 必须在 {DataArchiveOptions.MinSampleItemLimit}~{DataArchiveOptions.MaxSampleItemLimit} 之间")
                .ValidateOnStart();

            services.AddScoped<DataArchivePlanner>();
            services.AddScoped<DataArchiveCheckpointStore>();
            services.AddScoped<DataArchiveExecutor>();
            services.AddScoped<DataArchiveHostedWorker>();
        }

        /// <summary>
        /// 注册备份治理能力。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterBackupGovernanceServices(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<BackupOptions>()
                .Configure(options => ApplyBackupOptions(options, configuration))
                .Validate(
                    static options => options.PollIntervalMinutes is >= BackupOptions.MinPollIntervalMinutes and <= BackupOptions.MaxPollIntervalMinutes,
                    $"PollIntervalMinutes 必须在 {BackupOptions.MinPollIntervalMinutes}~{BackupOptions.MaxPollIntervalMinutes} 之间")
                .Validate(
                    static options => options.MaxAllowedBackupAgeHours is >= BackupOptions.MinMaxAllowedBackupAgeHours and <= BackupOptions.MaxMaxAllowedBackupAgeHours,
                    $"MaxAllowedBackupAgeHours 必须在 {BackupOptions.MinMaxAllowedBackupAgeHours}~{BackupOptions.MaxMaxAllowedBackupAgeHours} 之间")
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.BackupDirectory),
                    "Persistence:Backup:BackupDirectory 不允许为空")
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.BackupFilePrefix),
                    "Persistence:Backup:BackupFilePrefix 不允许为空")
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.RestoreRunbookDirectory),
                    "Persistence:Backup:RestoreRunbookDirectory 不允许为空")
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.DrillRecordDirectory),
                    "Persistence:Backup:DrillRecordDirectory 不允许为空")
                .ValidateOnStart();

            services.TryAddSingleton<RestoreDrillPlanner>();
            services.TryAddSingleton<BackupVerificationService>();
        }

        /// <summary>
        /// 注册数据保留治理能力。
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="configuration">配置根。</param>
        private static void RegisterDataRetentionServices(IServiceCollection services, IConfiguration configuration) {
            services.AddOptions<DataRetentionOptions>()
                .Configure(options => ApplyDataRetentionOptions(options, configuration))
                .Validate(
                    static options => options.BatchSize is >= DataRetentionOptions.MinBatchSize and <= DataRetentionOptions.MaxBatchSize,
                    $"BatchSize 必须在 {DataRetentionOptions.MinBatchSize}~{DataRetentionOptions.MaxBatchSize} 之间")
                .Validate(
                    static options => options.PollIntervalMinutes is >= DataRetentionOptions.MinPollIntervalMinutes and <= DataRetentionOptions.MaxPollIntervalMinutes,
                    $"PollIntervalMinutes 必须在 {DataRetentionOptions.MinPollIntervalMinutes}~{DataRetentionOptions.MaxPollIntervalMinutes} 之间")
                .Validate(
                    static options => !options.IsEnabled || options.Policies.Count > 0,
                    "启用数据保留治理时至少需要配置一个策略")
                .Validate(
                    static options => options.Policies.All(static policy => DataRetentionPolicy.IsSupportedName(policy.Name)),
                    "Persistence:Retention:Policies:Name 存在不受支持的对象名称")
                .Validate(
                    static options => options.Policies.All(static policy => DataRetentionPolicy.IsValidRetentionDays(policy.RetentionDays)),
                    $"Persistence:Retention:Policies:RetentionDays 必须在 {DataRetentionPolicy.MinRetentionDays}~{DataRetentionPolicy.MaxRetentionDays} 之间")
                .ValidateOnStart();

            services.TryAddSingleton<DataRetentionPlanner>();
            services.TryAddSingleton<DataRetentionExecutor>();
        }

        /// <summary>
        /// 应用批量缓冲写入配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyBufferedWriteOptions(BufferedWriteOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, $"{BufferedWriteOptions.SectionPath}:IsEnabled", options.IsEnabled);
            options.ChannelCapacity = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:ChannelCapacity", options.ChannelCapacity);
            options.BatchSize = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:BatchSize", options.BatchSize);
            options.FlushIntervalMilliseconds = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:FlushIntervalMilliseconds", options.FlushIntervalMilliseconds);
            options.MaxRetryCount = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:MaxRetryCount", options.MaxRetryCount);
            options.BackpressureRejectThreshold = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:BackpressureRejectThreshold", options.BackpressureRejectThreshold);
            options.DeadLetterCapacity = ReadIntSetting(configuration, $"{BufferedWriteOptions.SectionPath}:DeadLetterCapacity", options.DeadLetterCapacity);
        }

        /// <summary>
        /// 应用数据归档 dry-run 配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyDataArchiveOptions(DataArchiveOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, "Persistence:Archiving:IsEnabled", options.IsEnabled);
            options.WorkerPollIntervalSeconds = ReadIntSetting(configuration, "Persistence:Archiving:WorkerPollIntervalSeconds", options.WorkerPollIntervalSeconds);
            options.SampleItemLimit = ReadIntSetting(configuration, "Persistence:Archiving:SampleItemLimit", options.SampleItemLimit);
        }

        /// <summary>
        /// 应用备份治理配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyBackupOptions(BackupOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, $"{BackupOptions.SectionPath}:IsEnabled", options.IsEnabled);
            options.DryRun = ReadBooleanSetting(configuration, $"{BackupOptions.SectionPath}:DryRun", options.DryRun);
            options.PollIntervalMinutes = ReadIntSetting(configuration, $"{BackupOptions.SectionPath}:PollIntervalMinutes", options.PollIntervalMinutes);
            options.MaxAllowedBackupAgeHours = ReadIntSetting(configuration, $"{BackupOptions.SectionPath}:MaxAllowedBackupAgeHours", options.MaxAllowedBackupAgeHours);
            options.BackupDirectory = ReadStringSetting(configuration, $"{BackupOptions.SectionPath}:BackupDirectory", options.BackupDirectory);
            options.BackupFilePrefix = ReadStringSetting(configuration, $"{BackupOptions.SectionPath}:BackupFilePrefix", options.BackupFilePrefix);
            options.RestoreRunbookDirectory = ReadStringSetting(configuration, $"{BackupOptions.SectionPath}:RestoreRunbookDirectory", options.RestoreRunbookDirectory);
            options.DrillRecordDirectory = ReadStringSetting(configuration, $"{BackupOptions.SectionPath}:DrillRecordDirectory", options.DrillRecordDirectory);
        }

        /// <summary>
        /// 应用数据保留治理配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyDataRetentionOptions(DataRetentionOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, $"{DataRetentionOptions.SectionPath}:IsEnabled", options.IsEnabled);
            options.EnableGuard = ReadBooleanSetting(configuration, $"{DataRetentionOptions.SectionPath}:EnableGuard", options.EnableGuard);
            options.AllowDangerousActionExecution = ReadBooleanSetting(configuration, $"{DataRetentionOptions.SectionPath}:AllowDangerousActionExecution", options.AllowDangerousActionExecution);
            options.DryRun = ReadBooleanSetting(configuration, $"{DataRetentionOptions.SectionPath}:DryRun", options.DryRun);
            options.BatchSize = ReadIntSetting(configuration, $"{DataRetentionOptions.SectionPath}:BatchSize", options.BatchSize);
            options.PollIntervalMinutes = ReadIntSetting(configuration, $"{DataRetentionOptions.SectionPath}:PollIntervalMinutes", options.PollIntervalMinutes);
            var policySections = configuration.GetSection($"{DataRetentionOptions.SectionPath}:Policies").GetChildren().ToArray();
            if (policySections.Length == 0) {
                options.Policies = DataRetentionPolicy.CreateDefaultPolicies();
                return;
            }

            options.Policies = policySections
                .Select(section => new DataRetentionPolicy {
                    Name = (section["Name"] ?? string.Empty).Trim(),
                    RetentionDays = ReadIntSetting(section, "RetentionDays", DataRetentionPolicy.MinRetentionDays)
                })
                .ToArray();
        }

        /// <summary>
        /// 应用分表运行期巡检配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyShardingRuntimeInspectionOptions(ShardingRuntimeInspectionOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, $"{ShardingRuntimeInspectionOptions.SectionPath}:IsEnabled", options.IsEnabled);
            options.InspectionIntervalMinutes = ReadIntSetting(configuration, $"{ShardingRuntimeInspectionOptions.SectionPath}:InspectionIntervalMinutes", options.InspectionIntervalMinutes);
            options.ShouldCheckIndexes = ReadBooleanSetting(configuration, $"{ShardingRuntimeInspectionOptions.SectionPath}:ShouldCheckIndexes", options.ShouldCheckIndexes);
            options.ShouldCheckNextPeriodTables = ReadBooleanSetting(configuration, $"{ShardingRuntimeInspectionOptions.SectionPath}:ShouldCheckNextPeriodTables", options.ShouldCheckNextPeriodTables);
            options.ShouldCheckCapacity = ReadBooleanSetting(configuration, $"{ShardingRuntimeInspectionOptions.SectionPath}:ShouldCheckCapacity", options.ShouldCheckCapacity);
        }

        /// <summary>
        /// 应用分表预建计划配置。
        /// </summary>
        /// <param name="options">配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyShardingPrebuildOptions(ShardingPrebuildOptions options, IConfiguration configuration) {
            options.IsEnabled = ReadBooleanSetting(configuration, $"{ShardingPrebuildOptions.SectionPath}:IsEnabled", options.IsEnabled);
            options.DryRun = ReadBooleanSetting(configuration, $"{ShardingPrebuildOptions.SectionPath}:DryRun", options.DryRun);
            options.PrebuildAheadHours = ReadIntSetting(configuration, $"{ShardingPrebuildOptions.SectionPath}:PrebuildAheadHours", options.PrebuildAheadHours);
        }

        /// <summary>
        /// 应用数据库连接诊断配置。
        /// </summary>
        /// <param name="options">诊断配置实例。</param>
        /// <param name="configuration">配置根。</param>
        private static void ApplyDatabaseConnectionDiagnosticsOptions(DatabaseConnectionDiagnosticsOptions options, IConfiguration configuration) {
            options.IsWarmupEnabled = ReadBooleanSetting(configuration, $"{DatabaseConnectionDiagnosticsOptions.SectionPath}:IsWarmupEnabled", options.IsWarmupEnabled);
            options.WarmupConnectionCount = ReadIntSetting(configuration, $"{DatabaseConnectionDiagnosticsOptions.SectionPath}:WarmupConnectionCount", options.WarmupConnectionCount);
            options.ProbeTimeoutMilliseconds = ReadIntSetting(configuration, $"{DatabaseConnectionDiagnosticsOptions.SectionPath}:ProbeTimeoutMilliseconds", options.ProbeTimeoutMilliseconds);
            options.FailureThreshold = ReadIntSetting(configuration, $"{DatabaseConnectionDiagnosticsOptions.SectionPath}:FailureThreshold", options.FailureThreshold);
            options.RecoveryThreshold = ReadIntSetting(configuration, $"{DatabaseConnectionDiagnosticsOptions.SectionPath}:RecoveryThreshold", options.RecoveryThreshold);
        }

        /// <summary>
        /// 读取布尔配置，无法解析时保留默认值。
        /// </summary>
        /// <param name="configuration">配置根。</param>
        /// <param name="key">配置键。</param>
        /// <param name="fallbackValue">回退值。</param>
        /// <returns>布尔配置值。</returns>
        private static bool ReadBooleanSetting(IConfiguration configuration, string key, bool fallbackValue) {
            return bool.TryParse(configuration[key], out var parsedValue)
                ? parsedValue
                : fallbackValue;
        }

        /// <summary>
        /// 读取整数配置，无法解析时保留默认值。
        /// </summary>
        /// <param name="configuration">配置根。</param>
        /// <param name="key">配置键。</param>
        /// <param name="fallbackValue">回退值。</param>
        /// <returns>整数配置值。</returns>
        private static int ReadIntSetting(IConfiguration configuration, string key, int fallbackValue) {
            return int.TryParse(configuration[key], out var parsedValue)
                ? parsedValue
                : fallbackValue;
        }

        /// <summary>
        /// 读取字符串配置，无法解析时保留默认值。
        /// </summary>
        /// <param name="configuration">配置根。</param>
        /// <param name="key">配置键。</param>
        /// <param name="fallbackValue">回退值。</param>
        /// <returns>字符串配置值。</returns>
        private static string ReadStringSetting(IConfiguration configuration, string key, string fallbackValue) {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value)
                ? fallbackValue
                : value.Trim();
        }

        /// <summary>
        /// 配置 MySQL 场景下的 DbContext 选项（池化 DbContext 与池化工厂复用同一配置）。
        /// </summary>
        private static void ConfigureMySqlDbContextOptions(IServiceProvider sp, DbContextOptionsBuilder options) {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var interceptor = sp.GetRequiredService<SlowQueryCommandInterceptor>();
            var mySqlSessionInterceptor = sp.GetRequiredService<MySqlSessionBootstrapConnectionInterceptor>();
            var cs = cfg.GetConnectionString(ConfiguredProviderNames.MySql)!;
            var commandTimeoutSeconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var maxRetryCount = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
            var maxRetryDelaySeconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

            var serverVersion = ResolveMySqlServerVersion(cfg, cs, null);
            options.UseMySql(cs, serverVersion, mySqlOptions => {
                // 迁移程序集通常指向 Host 或 Infrastructure，按迁移放置位置调整
                // mySqlOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: maxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                    errorNumbersToAdd: null);
                mySqlOptions.CommandTimeout(commandTimeoutSeconds);
            });

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.AddInterceptors(interceptor, mySqlSessionInterceptor);
        }

        /// <summary>
        /// 按“配置优先、探测兜底”解析 MySQL ServerVersion。
        /// </summary>
        /// <param name="configuration">应用配置。</param>
        /// <param name="connectionString">MySQL 连接字符串。</param>
        /// <param name="warnCallback">可选告警回调（仅用于测试中捕获告警消息）。</param>
        /// <returns>可用于 EF Core UseMySql 的服务端版本对象。</returns>
        /// <remarks>
        /// 处理策略：
        /// 1) 若配置项 <c>Persistence:MySql:ServerVersion</c> 合法（Major &gt;= 5），直接使用；
        /// 2) 若配置缺失或非法，尝试 <c>ServerVersion.AutoDetect</c>；
        /// 3) 若探测失败，回退到 MySQL 8.0.0。
        /// </remarks>
        internal static ServerVersion ResolveMySqlServerVersion(IConfiguration configuration, string connectionString, Action<string>? warnCallback = null) {
            var configuredVersion = configuration["Persistence:MySql:ServerVersion"];
            if (!string.IsNullOrWhiteSpace(configuredVersion)) {
                if (Version.TryParse(configuredVersion, out var parsedVersion) && parsedVersion.Major >= 5) {
                    return new MySqlServerVersion(parsedVersion);
                }

                LogResolveWarning(
                    warnCallback,
                    "配置项 Persistence:MySql:ServerVersion 非法或不受支持（要求 Major>=5），将回退到自动探测，Value={ConfiguredServerVersion}",
                    configuredVersion);
            }

            try {
                return ServerVersion.AutoDetect(connectionString);
            }
            catch (Exception ex) {
                NLogLogger.Warn(ex, "MySQL ServerVersion 自动探测失败，将回退到默认版本 8.0.0");
                warnCallback?.Invoke("MySQL ServerVersion 自动探测失败，将回退到默认版本 8.0.0");
                return new MySqlServerVersion(new Version(8, 0, 0));
            }
        }

        /// <summary>
        /// 统一记录 MySQL ServerVersion 解析告警（NLog 结构化输出，可选回调接收格式化后的消息文本）。
        /// </summary>
        /// <param name="warnCallback">可选告警回调，接收格式化后的可读消息（用于测试断言）。</param>
        /// <param name="nlogTemplate">NLog 消息模板（含命名参数占位符）。</param>
        /// <param name="nlogArg">填入 NLog 模板占位符的参数值，同时用于格式化回调消息。</param>
        private static void LogResolveWarning(Action<string>? warnCallback, string nlogTemplate, string nlogArg) {
            NLogLogger.Warn(nlogTemplate, nlogArg);
            var placeholderIndex = nlogTemplate.IndexOf('{', StringComparison.Ordinal);
            var messagePrefix = placeholderIndex >= 0 ? nlogTemplate[..placeholderIndex] : nlogTemplate;
            warnCallback?.Invoke($"{messagePrefix}{nlogArg}");
        }

        /// <summary>
        /// 配置 SQL Server 场景下的 DbContext 选项（池化 DbContext 与池化工厂复用同一配置）。
        /// </summary>
        private static void ConfigureSqlServerDbContextOptions(IServiceProvider sp, DbContextOptionsBuilder options) {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var interceptor = sp.GetRequiredService<SlowQueryCommandInterceptor>();
            var cs = cfg.GetConnectionString(ConfiguredProviderNames.SqlServer)!;
            var commandTimeoutSeconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            var maxRetryCount = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryCount", 5);
            var maxRetryDelaySeconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(cfg, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);

            options.UseSqlServer(cs, sqlServerOptions => {
                // 迁移程序集通常指向 Host 或 Infrastructure，按迁移放置位置调整
                // sqlServerOptions.MigrationsAssembly("Zeye.Sorting.Hub.Host");
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: maxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                    errorNumbersToAdd: null);
                sqlServerOptions.CommandTimeout(commandTimeoutSeconds);
            });

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            options.AddInterceptors(interceptor);
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
        /// 获取 WebRequestAuditLog 体系按日分表治理需要关注的实体类型清单（与分表注册规则同源）。
        /// </summary>
        /// <returns>实体类型清单。</returns>
        public static IReadOnlyList<Type> GetWebRequestAuditLogPerDayShardingEntityTypes() {
            return WebRequestAuditLogPerDayShardingRules
                .Select(static rule => rule.EntityType)
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
            var localShardingStartTime = AutoTuningConfigurationReader.NormalizeToLocalTime(parcelShardingStartTime);

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
        /// 注册 WebRequestAuditLog 冷热模型分表规则（统一 StartedAt 按日分表）。
        /// </summary>
        /// <param name="shardingBuilder">分表构建器。</param>
        /// <param name="shardingStartTime">分表起始时间。</param>
        private static void ConfigureWebRequestAuditLogSharding(
            IShardingBuilder shardingBuilder,
            DateTime shardingStartTime) {
            var localShardingStartTime = AutoTuningConfigurationReader.NormalizeToLocalTime(shardingStartTime);
            shardingBuilder.SetDateSharding<WebRequestAuditLog>(
                shardingField: nameof(WebRequestAuditLog.StartedAt),
                expandByDateMode: ExpandByDateMode.PerDay,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
            shardingBuilder.SetDateSharding<WebRequestAuditLogDetail>(
                shardingField: nameof(WebRequestAuditLogDetail.StartedAt),
                expandByDateMode: ExpandByDateMode.PerDay,
                startTime: localShardingStartTime,
                sourceName: ShardingConstant.DefaultSource);
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

        /// <summary>获取分表起始时间（基于当前本地时间的整点对齐值）。</summary>
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
                return AutoTuningConfigurationReader.NormalizeToLocalTime(parsed);
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
