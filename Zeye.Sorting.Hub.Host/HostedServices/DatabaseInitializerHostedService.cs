using NLog;
using Polly;
using System;
using System.Linq;
using System.Text;
using Polly.Retry;
using EFCore.Sharding;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库初始化后台服务：启动时迁移 + 可选方言初始化
    /// </summary>
    public sealed class DatabaseInitializerHostedService : IHostedService {

        /// <summary>配置项缺失时用于占位展示的默认文本（与中文日志语境保持一致）。</summary>
        private const string NotConfiguredPlaceholder = "未配置";

        /// <summary>迁移失败策略配置键（通用，优先级最低）。可填写值:Degraded（降级运行）/ FailFast（快速失败）。</summary>
        private const string MigrationFailureStrategyConfigKey = "Persistence:Migration:FailureStrategy";

        /// <summary>迁移失败策略配置键（生产环境专用，优先级高于通用键）。可填写值:Degraded（降级运行）/ FailFast（快速失败）。</summary>
        private const string MigrationFailureStrategyProductionConfigKey = "Persistence:Migration:FailureStrategy:Production";

        /// <summary>迁移失败策略配置键（非生产环境专用，优先级高于通用键）。可填写值:Degraded（降级运行）/ FailFast（快速失败）。</summary>
        private const string MigrationFailureStrategyNonProductionConfigKey = "Persistence:Migration:FailureStrategy:NonProduction";

        /// <summary>迁移错误时是否阻断启动的配置键。可填写值:true / false。</summary>
        private const string FailStartupOnMigrationErrorConfigKey = "Persistence:Migration:FailStartupOnError";

        /// <summary>MySQL Provider 标识键。</summary>
        private const string MySqlProviderKey = "MySql";

        /// <summary>SQL Server Provider 标识键。</summary>
        private const string SqlServerProviderKey = "SqlServer";

        /// <summary>MySQL 方言显示名称。</summary>
        private const string DialectProviderNameMySql = "MySQL";

        /// <summary>SQL Server 方言显示名称。</summary>
        private const string DialectProviderNameSqlServer = "SQLServer";

        /// <summary>Provider 标识到连接字符串节点键名的映射表（不区分大小写）。</summary>
        private static readonly IReadOnlyDictionary<string, string> ProviderConnectionStringKeyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [MySqlProviderKey] = MySqlProviderKey,
            [SqlServerProviderKey] = SqlServerProviderKey,
            [DialectProviderNameMySql] = MySqlProviderKey,
            [DialectProviderNameSqlServer] = SqlServerProviderKey
        };

        /// <summary>数据持久化 Provider 类型配置键。可填写值:MySql / SqlServer。</summary>
        private const string PersistenceProviderConfigKey = "Persistence:Provider";

        /// <summary>是否启用启动期自动建库检查的配置键。可填写值:true / false。</summary>
        private const string EnsureDatabaseExistsEnabledConfigKey = "Persistence:DatabaseBootstrap:EnsureDatabaseExists:Enabled";

        /// <summary>自动建库隔离守卫开关配置键。可填写值:true / false。</summary>
        private const string EnsureDatabaseExistsIsolatorEnableGuardConfigKey = "Persistence:DatabaseBootstrap:EnsureDatabaseExists:Isolator:EnableGuard";

        /// <summary>自动建库是否允许执行危险动作的配置键。可填写值:true / false。</summary>
        private const string EnsureDatabaseExistsIsolatorAllowDangerousActionExecutionConfigKey = "Persistence:DatabaseBootstrap:EnsureDatabaseExists:Isolator:AllowDangerousActionExecution";

        /// <summary>自动建库 dry-run 模式开关配置键。可填写值:true / false。</summary>
        private const string EnsureDatabaseExistsIsolatorDryRunConfigKey = "Persistence:DatabaseBootstrap:EnsureDatabaseExists:Isolator:DryRun";

        /// <summary>启动时是否自动创建分表的配置键。可填写值:true / false。</summary>
        private const string CreateShardingTableOnStartingConfigKey = "Persistence:Sharding:CreateShardingTableOnStarting";

        /// <summary>Parcel 关联哈希分片模数配置键。可填写值:正整数（默认 16）。</summary>
        private const string ParcelRelatedHashShardingModConfigKey = "Persistence:Sharding:ParcelRelatedHashShardingMod";

        /// <summary>哈希扩容触发阈值配置键。可填写值:0~1 之间的小数（默认 0.8）。</summary>
        private const string HashShardingExpansionTriggerRatioConfigKey = "Persistence:Sharding:HashSharding:ExpansionTriggerRatio";

        /// <summary>哈希扩容文本计划配置键（兼容历史格式，新版推荐使用结构化配置项）。</summary>
        private const string HashShardingLegacyExpansionPlanConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan";

        /// <summary>哈希扩容当前模数配置键。可填写值:正整数。</summary>
        private const string HashShardingExpansionPlanCurrentModConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod";

        /// <summary>哈希扩容目标模数配置键。可填写值:正整数（应大于当前模数）。</summary>
        private const string HashShardingExpansionPlanTargetModConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod";

        /// <summary>哈希扩容阶段列表配置键。可填写值:逗号分隔的阶段描述串（如 "16→32,32→64"）。</summary>
        private const string HashShardingExpansionPlanStagesConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:Stages";

        /// <summary>分表预建窗口配置键。可填写值:正整数（小时数，默认 72）。</summary>
        private const string ShardingPrebuildWindowHoursConfigKey = "Persistence:Sharding:Governance:PrebuildWindowHours";

        /// <summary>分表治理 Runbook 配置键。可填写值:任意文档路径或外链 URL。</summary>
        private const string ShardingRunbookConfigKey = "Persistence:Sharding:Governance:Runbook";

        /// <summary>是否启用 WebRequestAuditLog 每日治理守卫的配置键。可填写值:true / false。</summary>
        private const string WebRequestAuditLogPerDayGuardEnabledConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:EnablePerDayGuard";

        /// <summary>是否启用 WebRequestAuditLog 历史分表保留治理的配置键。可填写值:true / false。</summary>
        private const string WebRequestAuditLogRetentionEnabledConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Enabled";

        /// <summary>WebRequestAuditLog 历史分表保留数量配置键。可填写值:正整数（保留最近 N 个分片）。</summary>
        private const string WebRequestAuditLogRetentionKeepRecentShardCountConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:Retention:KeepRecentShardCount";

        /// <summary>WebRequestAuditLog 历史分表保留治理守卫开关配置键。可填写值:true / false。</summary>
        private const string WebRequestAuditLogRetentionIsolatorEnableGuardConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Isolator:EnableGuard";

        /// <summary>WebRequestAuditLog 历史分表保留是否允许执行危险动作的配置键。可填写值:true / false。</summary>
        private const string WebRequestAuditLogRetentionIsolatorAllowDangerousActionExecutionConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Isolator:AllowDangerousActionExecution";

        /// <summary>WebRequestAuditLog 历史分表保留 dry-run 模式开关配置键。可填写值:true / false。</summary>
        private const string WebRequestAuditLogRetentionIsolatorDryRunConfigKey = "Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Isolator:DryRun";

        /// <summary>是否启用分表关键索引一致性审计的配置键。可填写值:true / false。</summary>
        private const string ShardingCriticalIndexAuditEnabledConfigKey = "Persistence:Sharding:Governance:CriticalIndexAudit:Enabled";

        /// <summary>关键索引缺失时是否阻断启动的配置键。可填写值:true / false。</summary>
        private const string ShardingCriticalIndexAuditBlockOnMissingConfigKey = "Persistence:Sharding:Governance:CriticalIndexAudit:BlockOnMissing";

        /// <summary>DI 服务提供程序，用于在启动阶段按需解析作用域服务。</summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 应用配置源。
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// NLog 记录器（用于规则化异常日志输出）。
        /// </summary>
        private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

        /// <summary>数据库方言适配器，提供 Provider 差异化的 DDL/DML 实现。</summary>
        private readonly IDatabaseDialect _dialect;

        /// <summary>当前宿主环境名称。</summary>
        private readonly string _environmentName;

        /// <summary>是否生产环境。</summary>
        private readonly bool _isProductionEnvironment;

        /// <summary>迁移失败策略。</summary>
        private readonly MigrationFailureMode _migrationFailureMode;

        /// <summary>启动时是否自动创建分表。</summary>
        private readonly bool _createShardingTableOnStarting;

        /// <summary>Parcel 关联哈希分片模数。</summary>
        private readonly int _parcelRelatedHashShardingMod;

        /// <summary>哈希扩容触发阈值（0~1）。</summary>
        private readonly decimal _hashShardingExpansionTriggerRatio;

        /// <summary>哈希扩容当前模数（结构化）。</summary>
        private readonly int _hashShardingExpansionCurrentMod;

        /// <summary>哈希扩容目标模数（结构化）。</summary>
        private readonly int _hashShardingExpansionTargetMod;

        /// <summary>哈希扩容阶段列表（结构化）。</summary>
        private readonly IReadOnlyList<string> _hashShardingExpansionStages;

        /// <summary>哈希扩容文本计划（兼容历史配置）。</summary>
        private readonly string _hashShardingLegacyExpansionPlan;

        /// <summary>分表预建窗口（小时）。</summary>
        private readonly int _shardingPrebuildWindowHours;

        /// <summary>分表治理 Runbook 标识（文档路径或链接）。</summary>
        private readonly string _shardingRunbook;

        /// <summary>是否启用 WebRequestAuditLog PerDay 治理守卫。</summary>
        private readonly bool _enableWebRequestAuditLogPerDayGuard;

        /// <summary>是否启用 WebRequestAuditLog 历史分表保留治理。</summary>
        private readonly bool _enableWebRequestAuditLogRetention;

        /// <summary>WebRequestAuditLog 历史分表保留数量（按逻辑表维度）。</summary>
        private readonly int _webRequestAuditLogRetentionKeepRecentShardCount;

        /// <summary>WebRequestAuditLog 历史分表保留治理守卫开关。</summary>
        private readonly bool _webRequestAuditLogRetentionEnableGuard;

        /// <summary>WebRequestAuditLog 历史分表保留治理是否允许执行危险动作。</summary>
        private readonly bool _webRequestAuditLogRetentionAllowDangerousActionExecution;

        /// <summary>WebRequestAuditLog 历史分表保留治理是否 dry-run。</summary>
        private readonly bool _webRequestAuditLogRetentionDryRun;

        /// <summary>是否启用“物理分表关键索引一致性审计”。</summary>
        private readonly bool _enableCriticalIndexAudit;

        /// <summary>关键索引缺失时是否阻断启动。</summary>
        private readonly bool _blockOnCriticalIndexMissing;

        /// <summary>自动调谐观测输出器。</summary>
        private readonly IAutoTuningObservability _observability;

        /// <summary>Parcel 分表策略决策快照。</summary>
        private readonly ParcelShardingStrategyDecision _parcelShardingStrategyDecision;

        /// <summary>Parcel 分表策略配置校验错误集合。</summary>
        private readonly IReadOnlyList<string> _parcelShardingStrategyValidationErrors;

        /// <summary>物理分表存在性探测器。</summary>
        private readonly IShardingPhysicalTableProbe _shardingPhysicalTableProbe;

        /// <summary>是否启用启动期自动建库检查。</summary>
        private readonly bool _ensureDatabaseExistsEnabled;

        /// <summary>自动建库隔离守卫开关。</summary>
        private readonly bool _ensureDatabaseExistsEnableGuard;

        /// <summary>自动建库是否允许执行危险动作。</summary>
        private readonly bool _ensureDatabaseExistsAllowDangerousActionExecution;

        /// <summary>自动建库是否启用 dry-run。</summary>
        private readonly bool _ensureDatabaseExistsDryRun;

        /// <summary>当前数据库 Provider 配置键。</summary>
        private readonly string? _databaseProviderKey;

        /// <summary>Polly 异步重试策略，用于迁移及 DDL 操作的瞬态故障恢复。</summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        /// <summary>迁移治理运行期状态存储。</summary>
        private readonly MigrationGovernanceStateStore _migrationGovernanceStateStore;

        /// <summary>
        /// 初始化数据库初始化器后台服务及其启动参数。
        /// </summary>
        /// <param name="serviceProvider">服务提供器。</param>
        /// <param name="dialect">数据库方言。</param>
        /// <param name="shardingPhysicalTableProbe">物理分表探测器。</param>
        /// <param name="hostEnvironment">宿主环境。</param>
        /// <param name="configuration">应用配置。</param>
        /// <param name="migrationGovernanceStateStore">迁移治理状态存储。</param>
        public DatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            IDatabaseDialect dialect,
            IShardingPhysicalTableProbe shardingPhysicalTableProbe,
            IHostEnvironment hostEnvironment,
            IConfiguration configuration,
            MigrationGovernanceStateStore migrationGovernanceStateStore) {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _dialect = dialect;
            _migrationGovernanceStateStore = migrationGovernanceStateStore;
            _environmentName = hostEnvironment.EnvironmentName;
            _isProductionEnvironment = hostEnvironment.IsProduction();
            _migrationFailureMode = ResolveMigrationFailureMode(configuration, _isProductionEnvironment);
            _createShardingTableOnStarting = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, CreateShardingTableOnStartingConfigKey, false);
            _parcelRelatedHashShardingMod = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, ParcelRelatedHashShardingModConfigKey, 16);
            _hashShardingExpansionTriggerRatio = AutoTuningConfigurationReader.GetDecimalInRangeOrDefault(configuration, HashShardingExpansionTriggerRatioConfigKey, 0.8m, 0m, 1m);
            _hashShardingExpansionCurrentMod = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, HashShardingExpansionPlanCurrentModConfigKey, _parcelRelatedHashShardingMod);
            _hashShardingExpansionTargetMod = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, HashShardingExpansionPlanTargetModConfigKey, _hashShardingExpansionCurrentMod * 2);
            _hashShardingExpansionStages = ResolveShardingExpansionPlanStages(configuration);
            _hashShardingLegacyExpansionPlan = NormalizeOptionalTextOrPlaceholder(configuration[HashShardingLegacyExpansionPlanConfigKey], NotConfiguredPlaceholder);
            _shardingPrebuildWindowHours = AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, ShardingPrebuildWindowHoursConfigKey, 72);
            _shardingRunbook = NormalizeOptionalTextOrPlaceholder(configuration[ShardingRunbookConfigKey], NotConfiguredPlaceholder);
            _enableWebRequestAuditLogPerDayGuard = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, WebRequestAuditLogPerDayGuardEnabledConfigKey, true);
            _enableWebRequestAuditLogRetention = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, WebRequestAuditLogRetentionEnabledConfigKey, true);
            _webRequestAuditLogRetentionKeepRecentShardCount = ResolveWebRequestAuditLogRetentionKeepRecentShardCount(configuration);
            _webRequestAuditLogRetentionEnableGuard = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, WebRequestAuditLogRetentionIsolatorEnableGuardConfigKey, true);
            _webRequestAuditLogRetentionAllowDangerousActionExecution = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, WebRequestAuditLogRetentionIsolatorAllowDangerousActionExecutionConfigKey, false);
            _webRequestAuditLogRetentionDryRun = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, WebRequestAuditLogRetentionIsolatorDryRunConfigKey, true);
            _enableCriticalIndexAudit = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, ShardingCriticalIndexAuditEnabledConfigKey, true);
            _blockOnCriticalIndexMissing = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, ShardingCriticalIndexAuditBlockOnMissingConfigKey, true);
            _observability = serviceProvider.GetService<IAutoTuningObservability>() ?? new NullAutoTuningObservability();
            var parcelShardingStrategyEvaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
            _parcelShardingStrategyDecision = parcelShardingStrategyEvaluation.Decision;
            _parcelShardingStrategyValidationErrors = parcelShardingStrategyEvaluation.ValidationErrors;
            _shardingPhysicalTableProbe = shardingPhysicalTableProbe;
            _ensureDatabaseExistsEnabled = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, EnsureDatabaseExistsEnabledConfigKey, true);
            _ensureDatabaseExistsEnableGuard = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, EnsureDatabaseExistsIsolatorEnableGuardConfigKey, true);
            _ensureDatabaseExistsAllowDangerousActionExecution = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, EnsureDatabaseExistsIsolatorAllowDangerousActionExecutionConfigKey, false);
            _ensureDatabaseExistsDryRun = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, EnsureDatabaseExistsIsolatorDryRunConfigKey, false);
            var providerRaw = configuration[PersistenceProviderConfigKey];
            _databaseProviderKey = ResolveProviderConnectionStringKey(providerRaw, _dialect.ProviderName);

            _retryPolicy = Policy
                .Handle<Exception>(ex => ex is not OperationCanceledException and not ShardingGovernanceGuardException)
                .WaitAndRetryAsync(
                    retryCount: 6,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Min(30, 2 * attempt)),
                    onRetry: (ex, ts, attempt, _) => {
                        NLogLogger.Warn(ex,
                            "数据库初始化重试中，Attempt={Attempt}, DelaySeconds={DelaySeconds}, Provider={Provider}",
                            attempt, ts.TotalSeconds, _dialect.ProviderName);
                    });
        }

        /// <summary>
        /// 兼容历史测试调用的构造函数（第二参数仅用于签名兼容，不参与日志输出）。
        /// </summary>
        /// <param name="serviceProvider">服务提供器。</param>
        /// <param name="legacyLogger">历史日志参数（占位）。</param>
        /// <param name="dialect">数据库方言。</param>
        /// <param name="shardingPhysicalTableProbe">物理分表探测器。</param>
        /// <param name="hostEnvironment">宿主环境。</param>
        /// <param name="configuration">应用配置。</param>
        /// <param name="migrationGovernanceStateStore">迁移治理状态存储。</param>
        public DatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            object legacyLogger,
            IDatabaseDialect dialect,
            IShardingPhysicalTableProbe shardingPhysicalTableProbe,
            IHostEnvironment hostEnvironment,
            IConfiguration configuration,
            MigrationGovernanceStateStore migrationGovernanceStateStore)
            : this(serviceProvider, dialect, shardingPhysicalTableProbe, hostEnvironment, configuration, migrationGovernanceStateStore) {
            ArgumentNullException.ThrowIfNull(legacyLogger);
        }

        /// <summary>服务启动入口：依序执行数据库迁移、方言初始化、分表预建等引导流程。</summary>
        public async Task StartAsync(CancellationToken cancellationToken) {
            try {
                AuditShardingGovernance();

                await _retryPolicy.ExecuteAsync(async (ct) => {
                    await EnsureDatabaseExistsAsync(ct);
                    await ValidateShardingGovernanceGuardAsync(ct);
                    var migrationPlan = _migrationGovernanceStateStore.GetLatestPlan();
                    var migrationExecutionRecord = _migrationGovernanceStateStore.GetLatestExecutionRecord();
                    if (migrationExecutionRecord is not null
                        && migrationExecutionRecord.IsEnabled
                        && !migrationExecutionRecord.ShouldApplyMigrations) {
                        NLogLogger.Warn(
                            "数据库迁移已由迁移治理拦截，Provider={Provider}, Environment={Environment}, Status={Status}, Reason={Reason}",
                            _dialect.ProviderName,
                            _environmentName,
                            migrationExecutionRecord.Status,
                            migrationExecutionRecord.SkipReason ?? migrationExecutionRecord.Summary);
                        return;
                    }

                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();

                    try {
                        NLogLogger.Info("开始执行数据库迁移，Provider={Provider}", _dialect.ProviderName);
                        await db.Database.MigrateAsync(ct);
                        NLogLogger.Info("数据库迁移完成，Provider={Provider}", _dialect.ProviderName);
                        if (migrationPlan is not null && migrationPlan.IsEnabled) {
                            _migrationGovernanceStateStore.SetLatestExecutionRecord(
                                MigrationExecutionRecord.CreateSucceeded(migrationPlan, "数据库迁移执行完成。"));
                        }
                    }
                    catch (Exception ex) {
                        if (migrationPlan is not null && migrationPlan.IsEnabled) {
                            _migrationGovernanceStateStore.SetLatestExecutionRecord(
                                MigrationExecutionRecord.CreateFailed(migrationPlan, ex.Message, "数据库迁移执行失败。"));
                        }

                        throw;
                    }

                    await AssertMigrationConsistencyAsync(db, ct);

                    foreach (var sql in _dialect.GetOptionalBootstrapSql()) {
                        try {
                            await db.Database.ExecuteSqlRawAsync(sql, ct);
                        }
                        catch (Exception ex) {
                            NLogLogger.Warn(ex,
                                "可选初始化 SQL 执行失败，已降级忽略，Provider={Provider}, Sql={Sql}",
                                _dialect.ProviderName, sql);
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException) {
                // 宿主正常停止时触发，不计为错误
                NLogLogger.Info("数据库初始化已因取消令牌中止，Provider={Provider}", _dialect.ProviderName);
            }
            catch (Exception ex) {
                if (ex is ShardingGovernanceGuardException) {
                    NLogLogger.Fatal(ex,
                        "[数据库初始化] 分表治理守卫触发，启动被阻断。Provider={Provider}, Environment={Environment}",
                        _dialect.ProviderName,
                        _environmentName);
                    throw;
                }

                // 重试耗尽或不可恢复异常：按配置决定是否阻断启动。
                if (_migrationFailureMode == MigrationFailureMode.FailFast) {
                    NLogLogger.Fatal(ex,
                        "[数据库初始化] 所有重试均失败，数据库连接不可用，且当前环境迁移策略为 FailFast，应用将终止启动。" +
                        "请检查连接字符串与数据库服务状态，Provider={Provider}, Environment={Environment}, ConfigKey={ConfigKey}",
                        _dialect.ProviderName,
                        _environmentName,
                        MigrationFailureStrategyConfigKey);
                    throw;
                }

                // 降级模式行为：
                //   - 应用继续运行，IHostedService 生命周期正常完成
                //   - 后续业务请求若访问数据库，将在 Repository/DbContext 层收到
                //     连接异常（如 MySqlException / SqlException），需在业务层自行处理
                //   - 应尽快修复数据库连接问题并重启应用以恢复正常
                //   - 请监控 logs/database-*.log 文件中的 Critical/Error 日志
                NLogLogger.Fatal(ex,
                    "[数据库初始化] 所有重试均失败，数据库连接不可用，服务将以降级模式运行。" +
                    "请检查连接字符串与数据库服务状态，Provider={Provider}, Environment={Environment}, Strategy={Strategy}",
                    _dialect.ProviderName,
                    _environmentName,
                    _migrationFailureMode);
            }
        }

        /// <summary>
        /// 解析“迁移失败是否阻断启动”配置。
        /// </summary>
        /// <remarks>
        /// 约定：
        /// <list type="bullet">
        ///   <item><description>未配置时默认 <c>false</c>，保持历史“降级启动”行为。</description></item>
        ///   <item><description>仅当配置值可解析且为 <c>true</c> 时，迁移失败才会阻断启动。</description></item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// 返回 <c>true</c> 表示迁移失败时阻断启动；返回 <c>false</c> 表示迁移失败后降级运行。
        /// </returns>
        internal static bool ResolveFailStartupOnMigrationError(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);
            var raw = configuration[FailStartupOnMigrationErrorConfigKey];
            return bool.TryParse(raw, out var value) && value;
        }

        /// <summary>
        /// 评估“启动期自动建库”隔离器决策。
        /// </summary>
        /// <param name="databaseMissing">目标数据库是否缺失。</param>
        /// <param name="enableGuard">是否启用守卫。</param>
        /// <param name="allowDangerousActionExecution">是否允许危险动作执行。</param>
        /// <param name="enableDryRun">是否启用 dry-run。</param>
        /// <returns>危险动作决策结果。</returns>
        internal static DangerousBatchActionResult EvaluateEnsureDatabaseExistsDecision(
            bool databaseMissing,
            bool enableGuard,
            bool allowDangerousActionExecution,
            bool enableDryRun) {
            var plannedCount = databaseMissing ? 1 : 0;
            var decision = databaseMissing
                ? ActionIsolationPolicy.Evaluate(
                    enableGuard,
                    allowDangerousActionExecution,
                    enableDryRun,
                    dangerousAction: true,
                    isRollback: false)
                : ActionIsolationDecision.Execute;
            var executedCount = databaseMissing && decision == ActionIsolationDecision.Execute ? 1 : 0;
            return new DangerousBatchActionResult {
                ActionName = "DatabaseBootstrap.EnsureDatabaseExists",
                Decision = decision,
                PlannedCount = plannedCount,
                ExecutedCount = executedCount,
                IsDryRun = decision == ActionIsolationDecision.DryRunOnly,
                IsBlockedByGuard = decision == ActionIsolationDecision.BlockedByGuard,
                CompensationBoundary = "数据库创建后如需回滚，需由运维执行受控 DROP DATABASE 或备份恢复。"
            };
        }

        /// <summary>
        /// 解析“迁移失败策略”：生产环境默认 FailFast，非生产默认 Degraded。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <param name="isProductionEnvironment">是否生产环境。</param>
        /// <returns>迁移失败策略。</returns>
        internal static MigrationFailureMode ResolveMigrationFailureMode(IConfiguration configuration, bool isProductionEnvironment) {
            ArgumentNullException.ThrowIfNull(configuration);

            var envSpecificKey = isProductionEnvironment
                ? MigrationFailureStrategyProductionConfigKey
                : MigrationFailureStrategyNonProductionConfigKey;
            if (TryParseMigrationFailureMode(configuration[envSpecificKey], out var envMode)) {
                return envMode;
            }

            if (TryParseMigrationFailureMode(configuration[MigrationFailureStrategyConfigKey], out var unifiedMode)) {
                return unifiedMode;
            }

            if (ResolveFailStartupOnMigrationError(configuration)) {
                return MigrationFailureMode.FailFast;
            }

            return isProductionEnvironment ? MigrationFailureMode.FailFast : MigrationFailureMode.Degraded;
        }

        /// <summary>将可选文本配置标准化为“非空白值或占位符”。</summary>
        internal static string NormalizeOptionalTextOrPlaceholder(string? value, string placeholder) {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? placeholder : normalized;
        }

        /// <summary>
        /// 解析哈希扩容阶段配置（结构化数组）；若未配置则返回空集合。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <returns>有序阶段清单。</returns>
        internal static IReadOnlyList<string> ResolveShardingExpansionPlanStages(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);
            return configuration.GetSection(HashShardingExpansionPlanStagesConfigKey)
                .GetChildren()
                .Select(static child => child.Value?.Trim())
                .Where(static stage => !string.IsNullOrWhiteSpace(stage))
                .Cast<string>()
                .ToArray();
        }

        /// <summary>
        /// 构建扩容计划摘要文本：优先使用结构化阶段，其次回退历史文本。
        /// </summary>
        /// <param name="currentMod">当前模数。</param>
        /// <param name="targetMod">目标模数。</param>
        /// <param name="stages">结构化阶段列表。</param>
        /// <param name="legacyPlan">历史文本计划。</param>
        /// <param name="placeholder">占位符。</param>
        /// <returns>用于审计日志的计划摘要。</returns>
        internal static string BuildExpansionPlanSummary(
            int currentMod,
            int targetMod,
            IReadOnlyList<string> stages,
            string legacyPlan,
            string placeholder) {
            ArgumentNullException.ThrowIfNull(stages);
            if (stages.Count > 0) {
                return $"{currentMod}->{targetMod}: {string.Join(" -> ", stages)}";
            }

            return string.Equals(legacyPlan, placeholder, StringComparison.Ordinal) ? placeholder : legacyPlan;
        }

        /// <summary>
        /// 尝试解析迁移失败策略文本。
        /// </summary>
        /// <param name="raw">原始配置文本。</param>
        /// <param name="mode">解析结果。</param>
        /// <returns>成功返回 true，否则 false。</returns>
        private static bool TryParseMigrationFailureMode(string? raw, out MigrationFailureMode mode) {
            mode = default;
            if (string.IsNullOrWhiteSpace(raw)) {
                return false;
            }

            var normalized = raw.Trim();
            if (normalized.Equals("FailFast", StringComparison.OrdinalIgnoreCase)) {
                mode = MigrationFailureMode.FailFast;
                return true;
            }

            if (normalized.Equals("Degraded", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Fallback", StringComparison.OrdinalIgnoreCase)) {
                mode = MigrationFailureMode.Degraded;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 解析当前 Provider 对应连接字符串键名。
        /// </summary>
        /// <param name="configuredProvider">配置中的 Provider 值。</param>
        /// <param name="dialectProviderName">方言 ProviderName。</param>
        /// <returns>连接字符串键名；不支持时返回 null。</returns>
        internal static string? ResolveProviderConnectionStringKey(string? configuredProvider, string dialectProviderName) {
            var configuredProviderKey = ResolveProviderConnectionStringKeyCore(configuredProvider);
            return configuredProviderKey ?? ResolveProviderConnectionStringKeyCore(dialectProviderName);
        }

        /// <summary>
        /// 解析单个 Provider 文本对应的连接字符串键名。
        /// </summary>
        /// <param name="providerRaw">Provider 原始值。</param>
        /// <returns>映射后的连接字符串键名；未命中返回 null。</returns>
        private static string? ResolveProviderConnectionStringKeyCore(string? providerRaw) {
            var normalized = providerRaw?.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) {
                return null;
            }

            return ProviderConnectionStringKeyMap.TryGetValue(normalized, out var mappedKey) ? mappedKey : null;
        }

        /// <summary>
        /// 启动期自动建库检查与执行。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken) {
            if (!_ensureDatabaseExistsEnabled) {
                NLogLogger.Info("启动期自动建库检查已禁用，Provider={Provider}, ConfigKey={ConfigKey}", _dialect.ProviderName, EnsureDatabaseExistsEnabledConfigKey);
                return;
            }

            if (string.IsNullOrWhiteSpace(_databaseProviderKey)) {
                NLogLogger.Warn("当前 Provider 不在自动建库支持范围内，跳过检查。Provider={Provider}", _dialect.ProviderName);
                return;
            }

            var connectionString = _configuration.GetConnectionString(_databaseProviderKey);
            if (string.IsNullOrWhiteSpace(connectionString)) {
                throw new InvalidOperationException($"缺少连接字符串：ConnectionStrings:{_databaseProviderKey}");
            }

            var databaseName = _dialect.ExtractDatabaseName(connectionString);
            await using var administrationConnection = _dialect.CreateAdministrationConnection(connectionString);
            var databaseExists = await _dialect.DatabaseExistsAsync(administrationConnection, databaseName, cancellationToken);
            var decisionResult = EvaluateEnsureDatabaseExistsDecision(
                databaseMissing: !databaseExists,
                enableGuard: _ensureDatabaseExistsEnableGuard,
                allowDangerousActionExecution: _ensureDatabaseExistsAllowDangerousActionExecution,
                enableDryRun: _ensureDatabaseExistsDryRun);

            NLogLogger.Info(
                "启动期自动建库治理审计：Decision={Decision}, PlannedCount={PlannedCount}, ExecutedCount={ExecutedCount}, Provider={Provider}, DatabaseName={DatabaseName}, CompensationBoundary={CompensationBoundary}",
                decisionResult.Decision,
                decisionResult.PlannedCount,
                decisionResult.ExecutedCount,
                _dialect.ProviderName,
                databaseName,
                decisionResult.CompensationBoundary);

            if (databaseExists || decisionResult.Decision != ActionIsolationDecision.Execute) {
                return;
            }

            await _dialect.CreateDatabaseAsync(administrationConnection, databaseName, cancellationToken);
            NLogLogger.Info("启动期自动建库已执行完成，Provider={Provider}, DatabaseName={DatabaseName}", _dialect.ProviderName, databaseName);
        }

        /// <summary>
        /// 迁移一致性守卫：确保 CodeFirst 模型与数据库迁移历史保持同步。
        /// </summary>
        /// <remarks>
        /// <para>
        /// EF Core 的 <c>MigrateAsync</c> 仅依赖 <c>__EFMigrationsHistory</c> 表来判断
        /// 哪些迁移已应用，因此存在以下风险场景：
        /// </para>
        /// <list type="bullet">
        ///   <item><description>
        ///     有人手工执行了 <c>ALTER TABLE / DROP TABLE</c> 等 DDL，但未修改 <c>__EFMigrationsHistory</c>；
        ///     此时 EF Core 认为迁移已完成，但实际表结构已偏离代码模型。
        ///   </description></item>
        ///   <item><description>
        ///     数据库被回滚/还原到旧快照，但迁移历史表仍记录了较新的迁移版本。
        ///   </description></item>
        /// </list>
        /// <para>
        /// 本方法通过以下检查尽早发现不一致，维护 CodeFirst 原则：
        /// </para>
        /// <list type="number">
        ///   <item><description>
        ///     调用 <c>GetPendingMigrationsAsync()</c>：若 <c>MigrateAsync()</c> 后仍有未应用迁移，
        ///     说明存在异常，记录 Critical 日志。
        ///   </description></item>
        ///   <item><description>
        ///     对比代码程序集中的迁移总数与 <c>__EFMigrationsHistory</c> 中的记录数：
        ///     若记录数多于代码（迁移历史被外部污染）或少于代码（迁移历史记录丢失），记录 Critical 日志。
        ///   </description></item>
        ///   <item><description>
        ///     调用 EF Core 9+ 提供的 <c>HasPendingModelChanges()</c>：检测当前代码模型是否存在
        ///     尚未通过 <c>dotnet ef migrations add</c> 生成迁移的变更（即模型快照与实体模型不一致），
        ///     若存在则记录 Critical 日志。这是 EF Core 8 所不具备的模型级一致性检测能力。
        ///   </description></item>
        /// </list>
        /// <para>
        /// ⚠️ 本方法无法自动修复手工 DDL 导致的表结构偏差。若检测到偏差，应通过 <c>dotnet ef migrations add</c>
        /// 生成新迁移来对齐数据库与代码模型，而非直接修改数据库。
        /// </para>
        /// </remarks>
        private async Task AssertMigrationConsistencyAsync(
            SortingHubDbContext db, CancellationToken cancellationToken) {
            // 检查 1：MigrateAsync 之后不应再有待应用迁移
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0) {
                // 迁移后仍有未应用项：记录 Critical 日志，继续执行后续可选 SQL。
                // 实际迁移 (MigrateAsync) 已运行完毕，该情况通常发生在：
                //   - 迁移文件在部署时被意外删除（History 表有记录但文件不存在）
                //   - 并发部署竞争导致 History 记录异常
                // 不抛出异常，以免阻止应用启动；但需运维人员关注 Critical 日志并排查原因。
                NLogLogger.Fatal(
                    "[CodeFirst 守卫] MigrateAsync 完成后仍存在 {PendingCount} 个未应用迁移，" +
                    "可能是并发写入或迁移文件缺失导致的不一致状态。" +
                    "未应用迁移：{PendingMigrations}，Provider={Provider}",
                    pending.Count,
                    string.Join(", ", pending),
                    _dialect.ProviderName);
                return;
            }

            // 检查 2：__EFMigrationsHistory 记录数应与代码中定义的迁移总数一致
            var allMigrationsInCode = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
            var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            // 在代码中定义但尚未在 __EFMigrationsHistory 中记录（通常不应在 MigrateAsync 后出现）
            var inCodeNotApplied = allMigrationsInCode.Except(appliedMigrations).OrderBy(static m => m).ToList();
            // 在 __EFMigrationsHistory 中记录但在代码中已不存在（可能是迁移文件被删除或历史表被外部污染）
            var appliedNotInCode = appliedMigrations.Except(allMigrationsInCode).OrderBy(static m => m).ToList();

            if (inCodeNotApplied.Count > 0 || appliedNotInCode.Count > 0) {
                var sb = new StringBuilder();
                sb.Append($"[CodeFirst 守卫] 迁移一致性异常：代码中定义了 {allMigrationsInCode.Count} 个迁移，");
                sb.Append($"__EFMigrationsHistory 中记录了 {appliedMigrations.Count} 个。");

                if (inCodeNotApplied.Count > 0) {
                    sb.AppendLine();
                    sb.Append($"  ▸ 代码中存在但未应用（{inCodeNotApplied.Count} 个）：");
                    sb.Append(string.Join(", ", inCodeNotApplied));
                }

                if (appliedNotInCode.Count > 0) {
                    sb.AppendLine();
                    sb.Append($"  ▸ 已应用但代码中不存在（{appliedNotInCode.Count} 个，可能为手工写入或迁移文件已删除）：");
                    sb.Append(string.Join(", ", appliedNotInCode));
                }

                sb.AppendLine();
                sb.Append("请通过 'dotnet ef migrations add' 生成新迁移以对齐模型，切勿直接修改数据库结构，以维护 CodeFirst 原则。");

                NLogLogger.Fatal(
                    "{Message} Provider={Provider}",
                    sb.ToString(),
                    _dialect.ProviderName);
            }
            else {
                NLogLogger.Info(
                    "[CodeFirst 守卫] 迁移一致性验证通过：共 {Count} 个迁移均已应用，Provider={Provider}",
                    appliedMigrations.Count,
                    _dialect.ProviderName);
            }

            // 检查 3（EF Core 9+）：检测代码模型是否存在尚未生成迁移的变更
            // HasPendingModelChanges() 对比当前 DbContext 的实体模型与最新迁移快照（ModelSnapshot），
            // 可发现手工修改实体类/配置后忘记执行 dotnet ef migrations add 的情况，
            // 这是 EF Core 8 所不具备的能力——EF Core 9 首次提供此 API。
            if (db.Database.HasPendingModelChanges()) {
                NLogLogger.Fatal(
                    "[CodeFirst 守卫] 检测到代码模型存在尚未生成迁移的变更（HasPendingModelChanges=true）。" +
                    "当前实体模型与最新迁移快照不一致，请执行 'dotnet ef migrations add <名称>' 生成新迁移，" +
                    "以维护 CodeFirst 原则。Provider={Provider}",
                    _dialect.ProviderName);
            }
            else {
                NLogLogger.Info(
                    "[CodeFirst 守卫] 模型变更检测通过（HasPendingModelChanges=false）：代码模型与迁移快照完全一致，Provider={Provider}",
                    _dialect.ProviderName);
            }
        }

        /// <summary>服务停止：暂无持有的后台资源需要释放，直接返回已完成任务。</summary>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// 启动期输出分表治理与容量预测相关审计信息。
        /// </summary>
        /// <remarks>
        /// 目标：
        /// 1) 显式提示 CreateShardingTableOnStarting=false 时必须执行预建流程；
        /// 2) 输出哈希分片模数、扩容触发阈值与迁移计划，降低“16→32”扩容认知断层；
        /// 3) 若未配置 Runbook，给出警告，推动制度化落地。
        /// </remarks>
        private void AuditShardingGovernance() {
            var expansionPlanSummary = BuildExpansionPlanSummary(
                _hashShardingExpansionCurrentMod,
                _hashShardingExpansionTargetMod,
                _hashShardingExpansionStages,
                _hashShardingLegacyExpansionPlan,
                NotConfiguredPlaceholder);

            NLogLogger.Info(
                "分表治理基线：Provider={Provider}, Environment={Environment}, MigrationFailureMode={MigrationFailureMode}, CreateShardingTableOnStarting={CreateShardingTableOnStarting}, ParcelRelatedHashShardingMod={ParcelRelatedHashShardingMod}, ExpansionTriggerRatio={ExpansionTriggerRatio:F2}, ExpansionPlan={ExpansionPlan}",
                _dialect.ProviderName,
                _environmentName,
                _migrationFailureMode,
                _createShardingTableOnStarting,
                _parcelRelatedHashShardingMod,
                _hashShardingExpansionTriggerRatio,
                expansionPlanSummary);

            NLogLogger.Info(
                "Parcel 分表策略决策：Mode={ParcelShardingMode}, EffectiveDateMode={ParcelEffectiveDateMode}, ThresholdAction={ThresholdAction}, ThresholdReached={ThresholdReached}, ObservationSource={ObservationSource}, DecisionReason={DecisionReason}",
                _parcelShardingStrategyDecision.Mode,
                _parcelShardingStrategyDecision.EffectiveDateMode,
                _parcelShardingStrategyDecision.ThresholdAction,
                _parcelShardingStrategyDecision.ThresholdReached,
                _parcelShardingStrategyDecision.VolumeObservation.Source,
                _parcelShardingStrategyDecision.Reason);
            NLogLogger.Info(
                "Parcel finer-granularity 扩展规划：ShouldPlanExtension={ShouldPlanExtension}, SuggestedMode={SuggestedMode}, Lifecycle={Lifecycle}, RequiresPrebuildGuard={RequiresPrebuildGuard}, PlanReason={PlanReason}",
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.ShouldPlanExtension,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.SuggestedMode,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.Lifecycle,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.RequiresPrebuildGuard,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.Reason);
            NLogLogger.Info(
                "分表关键索引一致性审计配置：Enabled={Enabled}, BlockOnMissing={BlockOnMissing}",
                _enableCriticalIndexAudit,
                _blockOnCriticalIndexMissing);
            NLogLogger.Info(
                "WebRequestAuditLog 治理配置：EnablePerDayGuard={EnablePerDayGuard}, RetentionEnabled={RetentionEnabled}, KeepRecentShardCount={KeepRecentShardCount}, RetentionEnableGuard={RetentionEnableGuard}, RetentionAllowDangerousActionExecution={RetentionAllowDangerousActionExecution}, RetentionDryRun={RetentionDryRun}",
                _enableWebRequestAuditLogPerDayGuard,
                _enableWebRequestAuditLogRetention,
                _webRequestAuditLogRetentionKeepRecentShardCount,
                _webRequestAuditLogRetentionEnableGuard,
                _webRequestAuditLogRetentionAllowDangerousActionExecution,
                _webRequestAuditLogRetentionDryRun);

            if (_parcelShardingStrategyDecision.Mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid) {
                NLogLogger.Info(
                    "Volume/Hybrid 语义说明：当前实现为“容量阈值驱动的时间粒度治理”，不是独立的按数据量物理分表引擎。");
            }

            if (_parcelShardingStrategyValidationErrors.Count > 0) {
                NLogLogger.Error(
                    "检测到 Parcel 分表策略配置校验失败：{ValidationErrors}",
                    string.Join(" | ", _parcelShardingStrategyValidationErrors));
            }

            if (!_createShardingTableOnStarting) {
                NLogLogger.Warn(
                    "分表自动创建已关闭：Provider={Provider}, PrebuildWindowHours={PrebuildWindowHours}, Runbook={Runbook}",
                    _dialect.ProviderName,
                    _shardingPrebuildWindowHours,
                    _shardingRunbook);
            }

            if (string.Equals(_shardingRunbook, NotConfiguredPlaceholder, StringComparison.Ordinal)) {
                NLogLogger.Warn(
                    "分表治理 Runbook 未配置：请补充配置项 {RunbookKey}，并在发布前完成预建窗口演练。",
                    ShardingRunbookConfigKey);
            }
        }

        /// <summary>
        /// 分表治理程序化守卫：避免“仅日志提醒”导致生产漏配。
        /// </summary>
        /// <remarks>
        /// 规则：
        /// 1) 当启动自动建表关闭时，必须配置 Runbook；
        /// 2) 结构化扩容计划的 TargetMod 必须大于 CurrentMod；
        /// 3) 生产环境下结构化阶段列表不能为空。
        /// </remarks>
        private async Task ValidateShardingGovernanceGuardAsync(CancellationToken cancellationToken) {
            if (_parcelShardingStrategyValidationErrors.Count > 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表策略配置非法：{string.Join(" | ", _parcelShardingStrategyValidationErrors)}");
            }

            if (_createShardingTableOnStarting) {
                return;
            }

            if (_hashShardingExpansionTargetMod <= _hashShardingExpansionCurrentMod) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理配置非法：{HashShardingExpansionPlanTargetModConfigKey}({_hashShardingExpansionTargetMod}) 必须大于 {HashShardingExpansionPlanCurrentModConfigKey}({_hashShardingExpansionCurrentMod})。");
            }

            if (string.Equals(_shardingRunbook, NotConfiguredPlaceholder, StringComparison.Ordinal)) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：当 {CreateShardingTableOnStartingConfigKey}=false 时，必须配置 {ShardingRunbookConfigKey}。");
            }

            if (_isProductionEnvironment && _hashShardingExpansionStages.Count == 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：生产环境要求使用结构化扩容计划，请至少配置 {HashShardingExpansionPlanStagesConfigKey}:0。");
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            var governanceGroups = ResolvePerDayGovernanceGroups(
                dbContext: db,
                parcelShardingDecision: _parcelShardingStrategyDecision,
                enableWebRequestAuditLogPerDayGuard: _enableWebRequestAuditLogPerDayGuard);
            var schemaName = ResolvePerDayPhysicalTableProbeSchemaName(_dialect.ProviderName);

            if (!_enableWebRequestAuditLogRetention) {
                return;
            }

            var webRequestAuditLogCandidates = await ResolveWebRequestAuditLogRetentionCandidatesFromMetadataAsync(
                db,
                governanceGroups,
                schemaName,
                cancellationToken);
            var webRequestAuditLogRetentionResult = EvaluateWebRequestAuditLogRetentionDecision(
                candidateCount: webRequestAuditLogCandidates.CandidateCount,
                enableGuard: _webRequestAuditLogRetentionEnableGuard,
                allowDangerousActionExecution: _webRequestAuditLogRetentionAllowDangerousActionExecution,
                enableDryRun: _webRequestAuditLogRetentionDryRun);
            var executedRetentionCount = await ExecuteWebRequestAuditLogRetentionAsync(
                db,
                schemaName,
                webRequestAuditLogCandidates.CandidatePhysicalTableNames,
                webRequestAuditLogRetentionResult,
                cancellationToken);
            webRequestAuditLogRetentionResult = new DangerousBatchActionResult {
                ActionName = webRequestAuditLogRetentionResult.ActionName,
                Decision = webRequestAuditLogRetentionResult.Decision,
                PlannedCount = webRequestAuditLogRetentionResult.PlannedCount,
                ExecutedCount = executedRetentionCount,
                IsDryRun = webRequestAuditLogRetentionResult.IsDryRun,
                IsBlockedByGuard = webRequestAuditLogRetentionResult.IsBlockedByGuard,
                CompensationBoundary = webRequestAuditLogRetentionResult.CompensationBoundary
            };
            NLogLogger.Info(
                "WebRequestAuditLog 历史分表保留治理评估：ActionName={ActionName}, Decision={Decision}, PlannedCount={PlannedCount}, ExecutedCount={ExecutedCount}, IsDryRun={IsDryRun}, IsBlockedByGuard={IsBlockedByGuard}, KeepRecentShardCount={KeepRecentShardCount}, CompensationBoundary={CompensationBoundary}",
                webRequestAuditLogRetentionResult.ActionName,
                webRequestAuditLogRetentionResult.Decision,
                webRequestAuditLogRetentionResult.PlannedCount,
                webRequestAuditLogRetentionResult.ExecutedCount,
                webRequestAuditLogRetentionResult.IsDryRun,
                webRequestAuditLogRetentionResult.IsBlockedByGuard,
                _webRequestAuditLogRetentionKeepRecentShardCount,
                webRequestAuditLogRetentionResult.CompensationBoundary);
        }

        /// <summary>
        /// 审计 PerDay 物理分表关键索引一致性（仅探测/记录，不执行 DDL）。
        /// </summary>
        /// <param name="db">数据库上下文。</param>
        /// <param name="physicalTableNames">待审计物理表名集合。</param>
        /// <param name="schemaName">探测 schema。</param>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失索引映射（Key=物理表名，Value=缺失索引名集合）。</returns>
        private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> AuditPerDayShardCriticalIndexesAsync(
            SortingHubDbContext db,
            IReadOnlyList<string> physicalTableNames,
            string? schemaName,
            string providerName,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(physicalTableNames);
            var blockingCriticalIndexesByLogicalTable = ResolveCriticalIndexesByLogicalTableForProvider(providerName);
            var auditOnlyIndexesByLogicalTable = ResolveAuditOnlyIndexesByLogicalTableForProvider(providerName);
            var missingIndexMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var physicalTableName in physicalTableNames) {
                if (!TryResolveLogicalBaseTableNameFromPhysicalTableName(physicalTableName, out var logicalBaseTableName)) {
                    continue;
                }
                if (!blockingCriticalIndexesByLogicalTable.TryGetValue(logicalBaseTableName, out var blockingCriticalIndexes)) {
                    blockingCriticalIndexes = [];
                }
                if (!auditOnlyIndexesByLogicalTable.TryGetValue(logicalBaseTableName, out var auditOnlyIndexes)) {
                    auditOnlyIndexes = [];
                }

                var missingBlockingIndexes = await _shardingPhysicalTableProbe.FindMissingIndexesAsync(
                    db,
                    schemaName,
                    physicalTableName,
                    blockingCriticalIndexes,
                    cancellationToken);
                var missingAuditOnlyIndexes = await _shardingPhysicalTableProbe.FindMissingIndexesAsync(
                    db,
                    schemaName,
                    physicalTableName,
                    auditOnlyIndexes,
                    cancellationToken);
                if (missingAuditOnlyIndexes.Count > 0) {
                    NLogLogger.Warn(
                        "分表关键索引一致性审计（仅审计项）发现缺失：PhysicalTable={PhysicalTable}, MissingIndexes={MissingIndexes}",
                        physicalTableName,
                        string.Join(", ", missingAuditOnlyIndexes));
                }

                if (missingBlockingIndexes.Count == 0) {
                    continue;
                }

                missingIndexMap[physicalTableName] = missingBlockingIndexes;
            }

            return missingIndexMap;
        }

        /// <summary>
        /// 解析当前 Provider 下需要审计的关键索引名集合。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <returns>可触发阻断的关键索引名集合。</returns>
        internal static IReadOnlyList<string> ResolveCriticalIndexesForProvider(string providerName) {
            return ResolveCriticalIndexesByLogicalTableForProvider(providerName)
                .SelectMany(static pair => pair.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 解析当前 Provider 下“逻辑表 -> 阻断关键索引”映射。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <returns>阻断关键索引映射。</returns>
        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveCriticalIndexesByLogicalTableForProvider(string providerName) {
            if (string.Equals(providerName, "MySQL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerName, "SQLServer", StringComparison.OrdinalIgnoreCase)) {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
                    ["Parcels"] = [
                        ParcelIndexNames.BagCodeScannedTime,
                        ParcelIndexNames.ActualChuteIdScannedTime,
                        ParcelIndexNames.TargetChuteIdScannedTime
                    ],
                    ["WebRequestAuditLogs"] = [
                        WebRequestAuditLogIndexNames.StartedAt,
                        WebRequestAuditLogIndexNames.StatusCodeStartedAt,
                        WebRequestAuditLogIndexNames.IsSuccessStartedAt
                    ],
                    ["WebRequestAuditLogDetails"] = [
                        WebRequestAuditLogIndexNames.DetailStartedAt
                    ]
                };
            }

            var mapping = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
                ["Parcels"] = [
                    ParcelIndexNames.BagCodeScannedTime,
                    ParcelIndexNames.ActualChuteIdScannedTime,
                    ParcelIndexNames.TargetChuteIdScannedTime
                ],
                ["WebRequestAuditLogs"] = [
                    WebRequestAuditLogIndexNames.StartedAt,
                    WebRequestAuditLogIndexNames.StatusCodeStartedAt,
                    WebRequestAuditLogIndexNames.IsSuccessStartedAt
                ],
                ["WebRequestAuditLogDetails"] = [
                    WebRequestAuditLogIndexNames.DetailStartedAt
                ]
            };
            return mapping;
        }

        /// <summary>
        /// 解析当前 Provider 下“仅审计不阻断”的索引名集合。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <returns>仅审计索引名集合。</returns>
        internal static IReadOnlyList<string> ResolveAuditOnlyIndexesForProvider(string providerName) {
            return ResolveAuditOnlyIndexesByLogicalTableForProvider(providerName)
                .SelectMany(static pair => pair.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 解析当前 Provider 下“逻辑表 -> 仅审计索引”映射。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <returns>仅审计索引映射。</returns>
        internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveAuditOnlyIndexesByLogicalTableForProvider(string providerName) {
            if (string.Equals(providerName, "MySQL", StringComparison.OrdinalIgnoreCase)) {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
                    ["Parcels"] = [ParcelIndexNames.BarCodesFullText]
                };
            }

            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 解析 PerDay 物理探测使用的 schema。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <returns>schema 名称；为空表示方言使用默认语义。</returns>
        internal static string? ResolvePerDayPhysicalTableProbeSchemaName(string providerName) {
            return string.Equals(providerName, "SQLServer", StringComparison.OrdinalIgnoreCase) ? "dbo" : null;
        }

        /// <summary>
        /// 从 EF 模型解析 PerDay 分表治理的基础逻辑表名清单。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <returns>基础逻辑表名清单。</returns>
        internal static IReadOnlyList<string> ResolvePerDayShardingBaseTableNames(SortingHubDbContext dbContext) {
            return ResolvePerDayShardingBaseTableNamesByEntityTypes(dbContext, PerDayShardingEntityTypes);
        }

        /// <summary>
        /// 判断是否需要执行 PerDay 治理组。
        /// </summary>
        /// <param name="decision">分表策略决策快照。</param>
        /// <returns>当前生效粒度为 PerDay 时返回 true。</returns>
        internal static bool ShouldEnforcePerDayPrebuildGuard(ParcelShardingStrategyDecision decision) {
            return decision.EffectiveDateMode == ExpandByDateMode.PerDay;
        }

        /// <summary>
        /// 评估 WebRequestAuditLog 历史分表保留治理决策（仅决策与审计语义，不执行真实 DDL）。
        /// </summary>
        /// <param name="candidateCount">本次候选处理数量（候选删除数）。</param>
        /// <param name="enableGuard">是否启用隔离守卫。</param>
        /// <param name="allowDangerousActionExecution">是否允许执行危险动作。</param>
        /// <param name="enableDryRun">是否开启 dry-run。</param>
        /// <returns>危险动作决策结果。</returns>
        internal static DangerousBatchActionResult EvaluateWebRequestAuditLogRetentionDecision(
            int candidateCount,
            bool enableGuard,
            bool allowDangerousActionExecution,
            bool enableDryRun) {
            if (candidateCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), "候选处理数量不能为负数。");
            }

            var decision = ActionIsolationPolicy.Evaluate(
                enableGuard,
                allowDangerousActionExecution,
                enableDryRun,
                dangerousAction: true,
                isRollback: false);
            var executedCount = decision == ActionIsolationDecision.Execute ? candidateCount : 0;
            return new DangerousBatchActionResult {
                ActionName = "WebRequestAuditLogHistoryRetentionCleanup",
                Decision = decision,
                PlannedCount = candidateCount,
                ExecutedCount = executedCount,
                IsDryRun = decision == ActionIsolationDecision.DryRunOnly,
                IsBlockedByGuard = decision == ActionIsolationDecision.BlockedByGuard,
                CompensationBoundary = "物理分表删除暂无自动回滚脚本，回滚需依赖备份/归档恢复。"
            };
        }

        /// <summary>
        /// PerDay 分表治理需要探测的实体类型清单（与分表注册同源）。
        /// </summary>
        /// <remarks>
        /// 当前通过 <see cref="BuildPerDayShardingEntityTypes"/> 从分表注册同源规则动态构造，
        /// 避免硬编码实体清单导致治理探测与注册规则漂移。
        /// </remarks>
        private static readonly IReadOnlyList<Type> PerDayShardingEntityTypes = BuildPerDayShardingEntityTypes();

        /// <summary>
        /// WebRequestAuditLog 分表治理需要探测的实体类型清单。
        /// </summary>
        private static readonly IReadOnlyList<Type> WebRequestAuditLogPerDayShardingEntityTypes = BuildWebRequestAuditLogPerDayShardingEntityTypes();

        /// <summary>
        /// 从分表注册同源规则动态构建 PerDay 探测实体类型清单。
        /// </summary>
        /// <returns>实体类型数组。</returns>
        private static IReadOnlyList<Type> BuildPerDayShardingEntityTypes() {
            return PersistenceServiceCollectionExtensions.GetParcelPerDayShardingEntityTypes();
        }

        /// <summary>
        /// 从 WebRequestAuditLog 冷热模型构建 PerDay 探测实体类型清单。
        /// </summary>
        /// <returns>实体类型数组。</returns>
        private static IReadOnlyList<Type> BuildWebRequestAuditLogPerDayShardingEntityTypes() {
            return PersistenceServiceCollectionExtensions.GetWebRequestAuditLogPerDayShardingEntityTypes();
        }

        /// <summary>
        /// 解析 WebRequestAuditLog 历史分表保留数量配置。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <returns>保留数量（必须为正整数）。</returns>
        internal static int ResolveWebRequestAuditLogRetentionKeepRecentShardCount(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);
            var raw = configuration[WebRequestAuditLogRetentionKeepRecentShardCountConfigKey];
            if (string.IsNullOrWhiteSpace(raw)) {
                return 7;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0) {
                throw new InvalidOperationException($"配置项 {WebRequestAuditLogRetentionKeepRecentShardCountConfigKey} 必须为大于 0 的整数，当前值：{raw}。");
            }

            return value;
        }

        /// <summary>
        /// 解析当前启动上下文下启用的 PerDay 治理组。
        /// </summary>
        /// <param name="dbContext">数据库上下文；为空时仅返回组与实体，不解析物理表名。</param>
        /// <param name="parcelShardingDecision">Parcel 分表决策。</param>
        /// <param name="enableWebRequestAuditLogPerDayGuard">是否启用 WebRequestAuditLog 守卫。</param>
        /// <returns>治理组清单。</returns>
        internal static IReadOnlyList<PerDayGovernanceGroup> ResolvePerDayGovernanceGroups(
            SortingHubDbContext? dbContext,
            ParcelShardingStrategyDecision parcelShardingDecision,
            bool enableWebRequestAuditLogPerDayGuard) {
            var groups = new List<PerDayGovernanceGroup>();
            if (ShouldEnforcePerDayPrebuildGuard(parcelShardingDecision)) {
                var baseTableNames = dbContext is null
                    ? Array.Empty<string>()
                    : ResolvePerDayShardingBaseTableNamesByEntityTypes(dbContext, PerDayShardingEntityTypes);
                groups.Add(new PerDayGovernanceGroup(
                    GroupName: "Parcel",
                    BaseTableNames: baseTableNames));
            }

            if (enableWebRequestAuditLogPerDayGuard) {
                var baseTableNames = dbContext is null
                    ? Array.Empty<string>()
                    : ResolvePerDayShardingBaseTableNamesByEntityTypes(dbContext, WebRequestAuditLogPerDayShardingEntityTypes);
                groups.Add(new PerDayGovernanceGroup(
                    GroupName: "WebRequestAuditLog",
                    BaseTableNames: baseTableNames));
            }

            return groups;
        }

        /// <summary>
        /// 从 EF 模型按指定实体类型集合解析基础逻辑表名。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="entityTypes">实体类型集合。</param>
        /// <returns>基础逻辑表名清单。</returns>
        private static IReadOnlyList<string> ResolvePerDayShardingBaseTableNamesByEntityTypes(
            SortingHubDbContext dbContext,
            IReadOnlyList<Type> entityTypes) {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(entityTypes);
            var baseTableNames = new List<string>(entityTypes.Count);
            foreach (var entityType in entityTypes) {
                var mappedEntityTypes = dbContext.Model
                    .GetEntityTypes()
                    .Where(modelEntityType => modelEntityType.ClrType == entityType)
                    .Select(modelEntityType => modelEntityType.GetTableName())
                    .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (mappedEntityTypes.Length != 1) {
                    throw new ShardingGovernanceGuardException(
                        $"分表治理守卫触发：无法从 EF 模型唯一解析 PerDay 探测目标表，EntityType={entityType.Name}, MatchedCount={mappedEntityTypes.Length}。");
                }

                baseTableNames.Add(mappedEntityTypes[0]);
            }

            return baseTableNames;
        }

        /// <summary>
        /// 从物理分表名中解析逻辑基础表名。
        /// </summary>
        /// <param name="physicalTableName">物理分表名。</param>
        /// <param name="baseTableName">解析出的逻辑基础表名。</param>
        /// <returns>成功解析返回 true。</returns>
        internal static bool TryResolveLogicalBaseTableNameFromPhysicalTableName(string physicalTableName, out string baseTableName) {
            baseTableName = string.Empty;
            if (string.IsNullOrWhiteSpace(physicalTableName)) {
                return false;
            }

            var separatorIndex = physicalTableName.LastIndexOf('_');
            if (separatorIndex <= 0 || separatorIndex >= physicalTableName.Length - 1) {
                return false;
            }

            var suffix = physicalTableName[(separatorIndex + 1)..];
            if (suffix.Length != 8 || !suffix.All(char.IsDigit)) {
                return false;
            }

            baseTableName = physicalTableName[..separatorIndex];
            return !string.IsNullOrWhiteSpace(baseTableName);
        }

        /// <summary>
        /// 估算 WebRequestAuditLog 历史分表保留候选数量（用于治理决策与审计）。
        /// </summary>
        /// <param name="requiredPrebuiltDates">预建窗口日期。</param>
        /// <param name="keepRecentShardCount">保留分表数量。</param>
        /// <param name="governanceGroups">治理组清单。</param>
        /// <returns>候选数量。</returns>
        internal static int EstimateWebRequestAuditLogRetentionCandidates(
            IReadOnlyList<DateTime> requiredPrebuiltDates,
            int keepRecentShardCount,
            IReadOnlyList<PerDayGovernanceGroup> governanceGroups) {
            ArgumentNullException.ThrowIfNull(requiredPrebuiltDates);
            ArgumentNullException.ThrowIfNull(governanceGroups);
            if (keepRecentShardCount <= 0) {
                return 0;
            }

            var webRequestAuditLogGroup = governanceGroups.FirstOrDefault(static group =>
                string.Equals(group.GroupName, "WebRequestAuditLog", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(webRequestAuditLogGroup.GroupName)
                || webRequestAuditLogGroup.BaseTableNames is null
                || webRequestAuditLogGroup.BaseTableNames.Count == 0) {
                return 0;
            }

            var candidateDateCount = Math.Max(0, requiredPrebuiltDates.Count - keepRecentShardCount);
            return candidateDateCount * webRequestAuditLogGroup.BaseTableNames.Count;
        }

        /// <summary>
        /// 根据真实分表元数据计算 WebRequestAuditLog 历史分表候选清单。
        /// </summary>
        /// <param name="db">数据库上下文。</param>
        /// <param name="governanceGroups">治理组清单。</param>
        /// <param name="schemaName">schema 名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>候选总数与物理表名清单。</returns>
        private async Task<WebRequestAuditLogRetentionCandidates> ResolveWebRequestAuditLogRetentionCandidatesFromMetadataAsync(
            SortingHubDbContext db,
            IReadOnlyList<PerDayGovernanceGroup> governanceGroups,
            string? schemaName,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(governanceGroups);
            var webRequestAuditLogGroup = governanceGroups.FirstOrDefault(static group =>
                string.Equals(group.GroupName, "WebRequestAuditLog", StringComparison.Ordinal));
            if (webRequestAuditLogGroup.BaseTableNames is null
                || webRequestAuditLogGroup.BaseTableNames.Count == 0) {
                return new WebRequestAuditLogRetentionCandidates(0, Array.Empty<string>());
            }

            var candidatePhysicalTableNames = new List<string>();
            foreach (var baseTableName in webRequestAuditLogGroup.BaseTableNames.Distinct(StringComparer.Ordinal)) {
                var allPhysicalTables = await ResolveExistingPerDayPhysicalTablesAsync(
                    db,
                    schemaName,
                    baseTableName,
                    cancellationToken);
                if (allPhysicalTables.Count <= _webRequestAuditLogRetentionKeepRecentShardCount) {
                    continue;
                }

                var deleteCount = allPhysicalTables.Count - _webRequestAuditLogRetentionKeepRecentShardCount;
                candidatePhysicalTableNames.AddRange(allPhysicalTables.Take(deleteCount));
            }

            return new WebRequestAuditLogRetentionCandidates(
                CandidateCount: candidatePhysicalTableNames.Count,
                CandidatePhysicalTableNames: candidatePhysicalTableNames.Distinct(StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// 解析指定逻辑表对应的已存在 PerDay 物理分表清单（按日期升序）。
        /// </summary>
        /// <param name="db">数据库上下文。</param>
        /// <param name="schemaName">schema 名称。</param>
        /// <param name="baseTableName">逻辑表名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>物理分表清单。</returns>
        private async Task<IReadOnlyList<string>> ResolveExistingPerDayPhysicalTablesAsync(
            SortingHubDbContext db,
            string? schemaName,
            string baseTableName,
            CancellationToken cancellationToken) {
            if (_shardingPhysicalTableProbe is not IBatchShardingPhysicalTableProbe batchProbe) {
                return Array.Empty<string>();
            }

            var physicalTables = await batchProbe.ListPhysicalTablesByBaseNameAsync(
                db,
                schemaName,
                baseTableName,
                cancellationToken);
            return physicalTables
                .Where(tableName => IsPerDayShardOfBaseTable(tableName, baseTableName))
                .OrderBy(static tableName => tableName, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 判断物理表是否属于指定逻辑基础表的 PerDay 分表。
        /// </summary>
        /// <param name="physicalTableName">物理表名。</param>
        /// <param name="baseTableName">逻辑基础表名。</param>
        /// <returns>属于指定逻辑表返回 true。</returns>
        private static bool IsPerDayShardOfBaseTable(string physicalTableName, string baseTableName) {
            if (string.IsNullOrWhiteSpace(physicalTableName) || string.IsNullOrWhiteSpace(baseTableName)) {
                return false;
            }

            var requiredPrefix = $"{baseTableName}_";
            if (!physicalTableName.StartsWith(requiredPrefix, StringComparison.Ordinal)) {
                return false;
            }

            var suffix = physicalTableName[requiredPrefix.Length..];
            return suffix.Length == 8 && suffix.All(char.IsDigit);
        }

        /// <summary>
        /// 执行 WebRequestAuditLog 历史分表保留删除链路（仅 Decision=Execute 时落地）。
        /// </summary>
        /// <param name="db">数据库上下文。</param>
        /// <param name="schemaName">schema 名称。</param>
        /// <param name="candidatePhysicalTableNames">候选物理表。</param>
        /// <param name="retentionDecision">治理决策。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实际执行删除数。</returns>
        private async Task<int> ExecuteWebRequestAuditLogRetentionAsync(
            SortingHubDbContext db,
            string? schemaName,
            IReadOnlyList<string> candidatePhysicalTableNames,
            DangerousBatchActionResult retentionDecision,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(candidatePhysicalTableNames);

            var tags = new Dictionary<string, string>(StringComparer.Ordinal) {
                ["action"] = retentionDecision.ActionName,
                ["decision"] = retentionDecision.Decision.ToString(),
                ["planned_count"] = retentionDecision.PlannedCount.ToString(CultureInfo.InvariantCulture),
                ["compensation_boundary"] = retentionDecision.CompensationBoundary
            };

            if (retentionDecision.Decision != ActionIsolationDecision.Execute) {
                _observability.EmitMetric("web_request_audit_log.retention.executed_count", 0d, tags);
                _observability.EmitEvent(
                    name: "web_request_audit_log.retention.skipped",
                    level: NLog.LogLevel.Info,
                    message: $"WebRequestAuditLog 历史分表保留未执行，Decision={retentionDecision.Decision}",
                    tags: tags);
                return 0;
            }

            if (candidatePhysicalTableNames.Count == 0) {
                _observability.EmitMetric("web_request_audit_log.retention.executed_count", 0d, tags);
                return 0;
            }

            var executedCount = 0;
            foreach (var physicalTableName in candidatePhysicalTableNames) {
                try {
                    var dropTableSql = BuildDropTableSql(_dialect.ProviderName, schemaName, physicalTableName);
                    await db.Database.ExecuteSqlRawAsync(dropTableSql, cancellationToken);
                    executedCount++;
                }
                catch (Exception ex) {
                    NLogLogger.Error(ex,
                        "WebRequestAuditLog 历史分表删除失败，PhysicalTable={PhysicalTable}, Decision={Decision}, CompensationBoundary={CompensationBoundary}",
                        physicalTableName,
                        retentionDecision.Decision,
                        retentionDecision.CompensationBoundary);
                    _observability.EmitEvent(
                        name: "web_request_audit_log.retention.failed",
                        level: NLog.LogLevel.Error,
                        message: $"WebRequestAuditLog 历史分表删除失败，PhysicalTable={physicalTableName}",
                        tags: new Dictionary<string, string>(tags, StringComparer.Ordinal) {
                            ["physical_table"] = physicalTableName
                        });
                    throw;
                }
            }

            _observability.EmitMetric("web_request_audit_log.retention.executed_count", executedCount, tags);
            _observability.EmitEvent(
                name: "web_request_audit_log.retention.executed",
                level: NLog.LogLevel.Info,
                message: $"WebRequestAuditLog 历史分表保留执行完成，ExecutedCount={executedCount}",
                tags: tags);
            return executedCount;
        }

        /// <summary>
        /// 构建物理分表删除 SQL。
        /// </summary>
        /// <param name="providerName">数据库提供器名称。</param>
        /// <param name="schemaName">schema 名称。</param>
        /// <param name="physicalTableName">物理表名。</param>
        /// <returns>DROP TABLE SQL。</returns>
        /// <exception cref="InvalidOperationException">Provider 不支持时抛出。</exception>
        private static string BuildDropTableSql(string providerName, string? schemaName, string physicalTableName) {
            if (!TryResolveLogicalBaseTableNameFromPhysicalTableName(physicalTableName, out _)) {
                throw new InvalidOperationException($"物理表名不符合 PerDay 分表命名规范：{physicalTableName}");
            }

            if (string.Equals(providerName, "MySQL", StringComparison.OrdinalIgnoreCase)) {
                return string.IsNullOrWhiteSpace(schemaName)
                    ? $"DROP TABLE IF EXISTS `{physicalTableName}`;"
                    : $"DROP TABLE IF EXISTS `{schemaName}`.`{physicalTableName}`;";
            }

            if (string.Equals(providerName, "SQLServer", StringComparison.OrdinalIgnoreCase)) {
                var safeSchemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName.Trim();
                return $"IF OBJECT_ID(N'[{safeSchemaName}].[{physicalTableName}]', N'U') IS NOT NULL DROP TABLE [{safeSchemaName}].[{physicalTableName}];";
            }

            throw new InvalidOperationException($"不支持的数据库提供器：{providerName}");
        }

    }
}
