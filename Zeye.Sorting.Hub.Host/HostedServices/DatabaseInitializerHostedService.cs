using Polly;
using System;
using System.Linq;
using System.Text;
using System.Globalization;
using Polly.Retry;
using System.Threading.Tasks;
using System.Collections.Generic;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Zeye.Sorting.Hub.Host.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库初始化后台服务：启动时迁移 + 可选方言初始化
    /// </summary>
    public sealed class DatabaseInitializerHostedService : IHostedService {
        /// <summary>配置项缺失时用于占位展示的默认文本（与中文日志语境保持一致）。</summary>
        private const string NotConfiguredPlaceholder = "未配置";
        private const string MigrationFailureStrategyConfigKey = "Persistence:Migration:FailureStrategy";
        private const string MigrationFailureStrategyProductionConfigKey = "Persistence:Migration:FailureStrategy:Production";
        private const string MigrationFailureStrategyNonProductionConfigKey = "Persistence:Migration:FailureStrategy:NonProduction";
        private const string FailStartupOnMigrationErrorConfigKey = "Persistence:Migration:FailStartupOnError";
        private const string CreateShardingTableOnStartingConfigKey = "Persistence:Sharding:CreateShardingTableOnStarting";
        private const string ParcelRelatedHashShardingModConfigKey = "Persistence:Sharding:ParcelRelatedHashShardingMod";
        private const string HashShardingExpansionTriggerRatioConfigKey = "Persistence:Sharding:HashSharding:ExpansionTriggerRatio";
        private const string HashShardingLegacyExpansionPlanConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan";
        private const string HashShardingExpansionPlanCurrentModConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod";
        private const string HashShardingExpansionPlanTargetModConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod";
        private const string HashShardingExpansionPlanStagesConfigKey = "Persistence:Sharding:HashSharding:ExpansionPlan:Stages";
        private const string ShardingPrebuildWindowHoursConfigKey = "Persistence:Sharding:Governance:PrebuildWindowHours";
        private const string ShardingRunbookConfigKey = "Persistence:Sharding:Governance:Runbook";
        private const string ShardingManualPrebuildGuardConfigKey = "Persistence:Sharding:Governance:EnableManualPrebuildGuard";
        private const string ShardingPrebuiltPerDayDatesConfigKey = "Persistence:Sharding:Governance:PrebuiltPerDayDates";
        /// <summary>
        /// 字段：_serviceProvider。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseInitializerHostedService> _logger;
        /// <summary>
        /// 字段：_dialect。
        /// </summary>
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
        /// <summary>是否启用“手工预建分表”治理守卫。</summary>
        private readonly bool _enableManualPrebuildGuard;
        /// <summary>手工预建模式下已完成预建的日分表日期（本地日期）。</summary>
        private readonly IReadOnlySet<DateTime> _prebuiltPerDayShardDates;
        /// <summary>日分表预建日期配置解析错误集合。</summary>
        private readonly IReadOnlyList<string> _prebuiltPerDayShardDateValidationErrors;
        /// <summary>Parcel 分表策略决策快照。</summary>
        private readonly ParcelShardingStrategyDecision _parcelShardingStrategyDecision;
        /// <summary>Parcel 分表策略配置校验错误集合。</summary>
        private readonly IReadOnlyList<string> _parcelShardingStrategyValidationErrors;
        /// <summary>物理分表存在性探测器。</summary>
        private readonly IShardingPhysicalTableProbe _shardingPhysicalTableProbe;

        /// <summary>
        /// 字段：_retryPolicy。
        /// </summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        public DatabaseInitializerHostedService(
            IServiceProvider serviceProvider,
            ILogger<DatabaseInitializerHostedService> logger,
            IDatabaseDialect dialect,
            IShardingPhysicalTableProbe shardingPhysicalTableProbe,
            IHostEnvironment hostEnvironment,
            IConfiguration configuration) {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dialect = dialect;
            _environmentName = hostEnvironment.EnvironmentName;
            _isProductionEnvironment = hostEnvironment.IsProduction();
            _migrationFailureMode = ResolveMigrationFailureMode(configuration, _isProductionEnvironment);
            _createShardingTableOnStarting = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, CreateShardingTableOnStartingConfigKey, false);
            _parcelRelatedHashShardingMod = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, ParcelRelatedHashShardingModConfigKey, 16);
            _hashShardingExpansionTriggerRatio = AutoTuningConfigurationHelper.GetDecimalInRangeOrDefault(configuration, HashShardingExpansionTriggerRatioConfigKey, 0.8m, 0m, 1m);
            _hashShardingExpansionCurrentMod = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, HashShardingExpansionPlanCurrentModConfigKey, _parcelRelatedHashShardingMod);
            _hashShardingExpansionTargetMod = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, HashShardingExpansionPlanTargetModConfigKey, _hashShardingExpansionCurrentMod * 2);
            _hashShardingExpansionStages = ResolveShardingExpansionPlanStages(configuration);
            _hashShardingLegacyExpansionPlan = NormalizeOptionalTextOrPlaceholder(configuration[HashShardingLegacyExpansionPlanConfigKey], NotConfiguredPlaceholder);
            _shardingPrebuildWindowHours = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, ShardingPrebuildWindowHoursConfigKey, 72);
            _shardingRunbook = NormalizeOptionalTextOrPlaceholder(configuration[ShardingRunbookConfigKey], NotConfiguredPlaceholder);
            _enableManualPrebuildGuard = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, ShardingManualPrebuildGuardConfigKey, true);
            var prebuiltPerDayShards = ResolvePrebuiltPerDayShardDates(configuration);
            _prebuiltPerDayShardDates = prebuiltPerDayShards.PrebuiltDates;
            _prebuiltPerDayShardDateValidationErrors = prebuiltPerDayShards.ValidationErrors;
            var parcelShardingStrategyEvaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
            _parcelShardingStrategyDecision = parcelShardingStrategyEvaluation.Decision;
            _parcelShardingStrategyValidationErrors = parcelShardingStrategyEvaluation.ValidationErrors;
            _shardingPhysicalTableProbe = shardingPhysicalTableProbe;

            _retryPolicy = Policy
                .Handle<Exception>(ex => ex is not OperationCanceledException and not ShardingGovernanceGuardException)
                .WaitAndRetryAsync(
                    retryCount: 6,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Min(30, 2 * attempt)),
                    onRetry: (ex, ts, attempt, _) => {
                        _logger.LogWarning(ex,
                            "数据库初始化重试中，Attempt={Attempt}, DelaySeconds={DelaySeconds}, Provider={Provider}",
                            attempt, ts.TotalSeconds, _dialect.ProviderName);
                    });
        }

        /// <summary>
        /// 执行逻辑：StartAsync。
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken) {
            try {
                AuditShardingGovernance();
                await ValidateShardingGovernanceGuardAsync(cancellationToken);

                await _retryPolicy.ExecuteAsync(async (ct) => {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();

                    _logger.LogInformation("开始执行数据库迁移，Provider={Provider}", _dialect.ProviderName);
                    await db.Database.MigrateAsync(ct);
                    _logger.LogInformation("数据库迁移完成，Provider={Provider}", _dialect.ProviderName);

                    await AssertMigrationConsistencyAsync(db, ct);

                    foreach (var sql in _dialect.GetOptionalBootstrapSql()) {
                        try {
                            await db.Database.ExecuteSqlRawAsync(sql, ct);
                        }
                        catch (Exception ex) {
                            _logger.LogWarning(ex,
                                "可选初始化 SQL 执行失败，已降级忽略，Provider={Provider}, Sql={Sql}",
                                _dialect.ProviderName, sql);
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException) {
                // 宿主正常停止时触发，不计为错误
                _logger.LogInformation("数据库初始化已因取消令牌中止，Provider={Provider}", _dialect.ProviderName);
            }
            catch (Exception ex) {
                if (ex is ShardingGovernanceGuardException) {
                    _logger.LogCritical(ex,
                        "[数据库初始化] 分表治理守卫触发，启动被阻断。Provider={Provider}, Environment={Environment}",
                        _dialect.ProviderName,
                        _environmentName);
                    throw;
                }

                // 重试耗尽或不可恢复异常：按配置决定是否阻断启动。
                if (_migrationFailureMode == MigrationFailureMode.FailFast) {
                    _logger.LogCritical(ex,
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
                _logger.LogCritical(ex,
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
        /// 日分表预建日期解析结果。
        /// </summary>
        /// <param name="PrebuiltDates">已预建日期集合（本地日期）。</param>
        /// <param name="ValidationErrors">解析错误集合。</param>
        internal readonly record struct PrebuiltPerDayShardDatesResolution(
            IReadOnlySet<DateTime> PrebuiltDates,
            IReadOnlyList<string> ValidationErrors);

        /// <summary>
        /// 解析手工预建的日分表日期清单（yyyy-MM-dd，本地时间语义）。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <returns>解析结果。</returns>
        internal static PrebuiltPerDayShardDatesResolution ResolvePrebuiltPerDayShardDates(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);

            var parsedDates = new HashSet<DateTime>();
            var validationErrors = new List<string>();
            foreach (var item in configuration.GetSection(ShardingPrebuiltPerDayDatesConfigKey).GetChildren()) {
                var raw = item.Value?.Trim();
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }

                if (!DateTime.TryParseExact(
                    raw,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsedDate)) {
                    validationErrors.Add($"配置项 {ShardingPrebuiltPerDayDatesConfigKey} 包含非法日期：{raw}。格式必须为 yyyy-MM-dd。");
                    continue;
                }

                parsedDates.Add(parsedDate.Date);
            }

            return new PrebuiltPerDayShardDatesResolution(
                PrebuiltDates: parsedDates,
                ValidationErrors: Array.AsReadOnly(validationErrors.ToArray()));
        }

        /// <summary>
        /// 计算预建窗口内必须完成预建的日分表日期清单（含当前日期）。
        /// </summary>
        /// <param name="localNow">当前本地时间。</param>
        /// <param name="prebuildWindowHours">预建窗口小时数。</param>
        /// <returns>必需预建的日期清单。</returns>
        internal static IReadOnlyList<DateTime> BuildRequiredPerDayShardDates(DateTime localNow, int prebuildWindowHours) {
            var windowEndDate = localNow.AddHours(prebuildWindowHours).Date;
            var cursor = localNow.Date;
            var requiredDates = new List<DateTime>();
            while (cursor <= windowEndDate) {
                requiredDates.Add(cursor);
                cursor = cursor.AddDays(1);
            }

            return requiredDates;
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
        /// 迁移一致性守卫：确保 CodeFirst 模型与数据库迁移历史保持同步。
        /// </summary>
        /// <remarks>
        /// <para>
        /// EF Core 的 <see cref="DatabaseFacade.MigrateAsync"/> 仅依赖 <c>__EFMigrationsHistory</c> 表来判断
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
                _logger.LogCritical(
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

                _logger.LogCritical(
                    "{Message} Provider={Provider}",
                    sb.ToString(),
                    _dialect.ProviderName);
            }
            else {
                _logger.LogInformation(
                    "[CodeFirst 守卫] 迁移一致性验证通过：共 {Count} 个迁移均已应用，Provider={Provider}",
                    appliedMigrations.Count,
                    _dialect.ProviderName);
            }

            // 检查 3（EF Core 9+）：检测代码模型是否存在尚未生成迁移的变更
            // HasPendingModelChanges() 对比当前 DbContext 的实体模型与最新迁移快照（ModelSnapshot），
            // 可发现手工修改实体类/配置后忘记执行 dotnet ef migrations add 的情况，
            // 这是 EF Core 8 所不具备的能力——EF Core 9 首次提供此 API。
            if (db.Database.HasPendingModelChanges()) {
                _logger.LogCritical(
                    "[CodeFirst 守卫] 检测到代码模型存在尚未生成迁移的变更（HasPendingModelChanges=true）。" +
                    "当前实体模型与最新迁移快照不一致，请执行 'dotnet ef migrations add <名称>' 生成新迁移，" +
                    "以维护 CodeFirst 原则。Provider={Provider}",
                    _dialect.ProviderName);
            }
            else {
                _logger.LogInformation(
                    "[CodeFirst 守卫] 模型变更检测通过（HasPendingModelChanges=false）：代码模型与迁移快照完全一致，Provider={Provider}",
                    _dialect.ProviderName);
            }
        }

        /// <summary>
        /// 执行逻辑：StopAsync。
        /// </summary>
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

            _logger.LogInformation(
                "分表治理基线：Provider={Provider}, Environment={Environment}, MigrationFailureMode={MigrationFailureMode}, CreateShardingTableOnStarting={CreateShardingTableOnStarting}, ParcelRelatedHashShardingMod={ParcelRelatedHashShardingMod}, ExpansionTriggerRatio={ExpansionTriggerRatio:F2}, ExpansionPlan={ExpansionPlan}",
                _dialect.ProviderName,
                _environmentName,
                _migrationFailureMode,
                _createShardingTableOnStarting,
                _parcelRelatedHashShardingMod,
                _hashShardingExpansionTriggerRatio,
                expansionPlanSummary);

            _logger.LogInformation(
                "Parcel 分表策略决策：Mode={ParcelShardingMode}, EffectiveDateMode={ParcelEffectiveDateMode}, ThresholdAction={ThresholdAction}, ThresholdReached={ThresholdReached}, ObservationSource={ObservationSource}, DecisionReason={DecisionReason}",
                _parcelShardingStrategyDecision.Mode,
                _parcelShardingStrategyDecision.EffectiveDateMode,
                _parcelShardingStrategyDecision.ThresholdAction,
                _parcelShardingStrategyDecision.ThresholdReached,
                _parcelShardingStrategyDecision.VolumeObservation.Source,
                _parcelShardingStrategyDecision.Reason);
            _logger.LogInformation(
                "Parcel finer-granularity 扩展规划：ShouldPlanExtension={ShouldPlanExtension}, SuggestedMode={SuggestedMode}, Lifecycle={Lifecycle}, RequiresPrebuildGuard={RequiresPrebuildGuard}, PlanReason={PlanReason}",
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.ShouldPlanExtension,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.SuggestedMode,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.Lifecycle,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.RequiresPrebuildGuard,
                _parcelShardingStrategyDecision.FinerGranularityExtensionPlan.Reason);

            if (_parcelShardingStrategyDecision.Mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid) {
                _logger.LogInformation(
                    "Volume/Hybrid 语义说明：当前实现为“容量阈值驱动的时间粒度治理”，不是独立的按数据量物理分表引擎。");
            }

            if (_parcelShardingStrategyValidationErrors.Count > 0) {
                _logger.LogError(
                    "检测到 Parcel 分表策略配置校验失败：{ValidationErrors}",
                    string.Join(" | ", _parcelShardingStrategyValidationErrors));
            }

            if (_prebuiltPerDayShardDateValidationErrors.Count > 0) {
                _logger.LogError(
                    "检测到日分表预建日期配置校验失败：{ValidationErrors}",
                    string.Join(" | ", _prebuiltPerDayShardDateValidationErrors));
            }

            if (!_createShardingTableOnStarting) {
                _logger.LogWarning(
                    "分表自动创建已关闭，必须依赖外部预建流程：Provider={Provider}, PrebuildWindowHours={PrebuildWindowHours}, Runbook={Runbook}, EnableManualPrebuildGuard={EnableManualPrebuildGuard}, PrebuiltPerDayDatesCount={PrebuiltPerDayDatesCount}",
                    _dialect.ProviderName,
                    _shardingPrebuildWindowHours,
                    _shardingRunbook,
                    _enableManualPrebuildGuard,
                    _prebuiltPerDayShardDates.Count);
            }

            if (string.Equals(_shardingRunbook, NotConfiguredPlaceholder, StringComparison.Ordinal)) {
                _logger.LogWarning(
                    "分表治理 Runbook 未配置：请补充配置项 {RunbookKey}，并在发布前完成预建窗口演练。",
                    ShardingRunbookConfigKey);
            }
        }

        /// <summary>
        /// 分表治理程序化守卫：避免“仅日志提醒”导致生产漏配。
        /// </summary>
        /// <remarks>
        /// 规则：
        /// 1) 当启动自动建表关闭且守卫开启时，必须配置 Runbook；
        /// 2) 结构化扩容计划的 TargetMod 必须大于 CurrentMod；
        /// 3) 生产环境且手工预建模式下，结构化阶段列表不能为空；
        /// 4) 当策略生效为 PerDay 且手工预建模式开启时，必须校验预建窗口内目标日表均已预建。
        /// </remarks>
        private async Task ValidateShardingGovernanceGuardAsync(CancellationToken cancellationToken) {
            if (_parcelShardingStrategyValidationErrors.Count > 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表策略配置非法：{string.Join(" | ", _parcelShardingStrategyValidationErrors)}");
            }

            if (_prebuiltPerDayShardDateValidationErrors.Count > 0) {
                throw new ShardingGovernanceGuardException(
                    $"日分表预建配置非法：{string.Join(" | ", _prebuiltPerDayShardDateValidationErrors)}");
            }

            if (_createShardingTableOnStarting || !_enableManualPrebuildGuard) {
                return;
            }

            if (_hashShardingExpansionTargetMod <= _hashShardingExpansionCurrentMod) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理配置非法：{HashShardingExpansionPlanTargetModConfigKey}({_hashShardingExpansionTargetMod}) 必须大于 {HashShardingExpansionPlanCurrentModConfigKey}({_hashShardingExpansionCurrentMod})。");
            }

            if (string.Equals(_shardingRunbook, NotConfiguredPlaceholder, StringComparison.Ordinal)) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：当 {CreateShardingTableOnStartingConfigKey}=false 且 {ShardingManualPrebuildGuardConfigKey}=true 时，必须配置 {ShardingRunbookConfigKey}。");
            }

            if (_isProductionEnvironment && _hashShardingExpansionStages.Count == 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：生产环境要求使用结构化扩容计划，请至少配置 {HashShardingExpansionPlanStagesConfigKey}:0。");
            }

            if (!ShouldEnforcePerDayPrebuildGuard(_parcelShardingStrategyDecision)) {
                return;
            }

            var requiredPrebuiltDates = BuildRequiredPerDayShardDates(DateTime.Now, _shardingPrebuildWindowHours);
            var missingDates = requiredPrebuiltDates
                .Where(requiredDate => !_prebuiltPerDayShardDates.Contains(requiredDate))
                .Select(requiredDate => requiredDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToArray();
            if (missingDates.Length > 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：当前策略已生效为 PerDay，且 {CreateShardingTableOnStartingConfigKey}=false。请先完成目标日表预建并配置 {ShardingPrebuiltPerDayDatesConfigKey}，缺失日期：{string.Join(", ", missingDates)}。");
            }

            var missingPhysicalTables = new List<string>();
            await using var scope = _serviceProvider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            var perDayShardingBaseTableNames = ResolvePerDayShardingBaseTableNames(db);
            var expectedPhysicalTables = BuildExpectedPerDayPhysicalTableNames(requiredPrebuiltDates, perDayShardingBaseTableNames);
            var schemaName = ResolvePerDayPhysicalTableProbeSchemaName(_dialect.ProviderName);
            if (_shardingPhysicalTableProbe is IBatchShardingPhysicalTableProbe batchShardingPhysicalTableProbe) {
                var missingPhysicalTableSet = await batchShardingPhysicalTableProbe.FindMissingTablesAsync(
                    db,
                    schemaName,
                    expectedPhysicalTables,
                    cancellationToken);
                missingPhysicalTables.AddRange(missingPhysicalTableSet);
            }
            else {
                foreach (var expectedPhysicalTable in expectedPhysicalTables) {
                    var exists = await _shardingPhysicalTableProbe.ExistsAsync(
                        db,
                        schemaName,
                        expectedPhysicalTable,
                        cancellationToken);
                    if (!exists) {
                        missingPhysicalTables.Add(expectedPhysicalTable);
                    }
                }
            }

            if (missingPhysicalTables.Count > 0) {
                throw new ShardingGovernanceGuardException(
                    $"分表治理守卫触发：当前策略已生效为 PerDay，且 {CreateShardingTableOnStartingConfigKey}=false。检测到配置清单已声明预建，但数据库物理表不存在。缺失物理表：{string.Join(", ", missingPhysicalTables)}。");
            }
        }

        /// <summary>
        /// 构建 PerDay 预建窗口对应的目标物理表名集合。
        /// </summary>
        /// <param name="requiredPrebuiltDates">窗口内必须预建的日期集合。</param>
        /// <returns>目标物理表名集合。</returns>
        /// <remarks>
        /// 规则说明：
        /// 1) 复用当前分表治理同源对象（Parcel 主表 + 已注册的日期型值对象表）；
        /// 2) 物理表名规则与 EFCore.Sharding 的按日扩展语义保持一致：BaseTable_yyyyMMdd；
        /// 3) 当前仅用于 PerDay 治理探测，不引入自动建表/迁移动作。
        /// </remarks>
        internal static IReadOnlyList<string> BuildExpectedPerDayPhysicalTableNames(
            IReadOnlyList<DateTime> requiredPrebuiltDates,
            IReadOnlyList<string> perDayShardingBaseTableNames) {
            ArgumentNullException.ThrowIfNull(requiredPrebuiltDates);
            ArgumentNullException.ThrowIfNull(perDayShardingBaseTableNames);
            var expectedPhysicalTables = new List<string>(requiredPrebuiltDates.Count * perDayShardingBaseTableNames.Count);
            foreach (var requiredDate in requiredPrebuiltDates) {
                foreach (var baseTableName in perDayShardingBaseTableNames) {
                    expectedPhysicalTables.Add(BuildPerDayPhysicalTableName(baseTableName, requiredDate));
                }
            }

            return expectedPhysicalTables;
        }

        /// <summary>
        /// 构建单个按日分表的物理表名。
        /// </summary>
        /// <param name="baseTableName">逻辑基础表名。</param>
        /// <param name="date">本地日期。</param>
        /// <returns>物理表名。</returns>
        internal static string BuildPerDayPhysicalTableName(string baseTableName, DateTime date) {
            return $"{baseTableName}_{date:yyyyMMdd}";
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
            ArgumentNullException.ThrowIfNull(dbContext);
            var baseTableNames = new List<string>(PerDayShardingEntityTypes.Count);
            foreach (var entityType in PerDayShardingEntityTypes) {
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
        /// 判断是否需要执行 PerDay 预建窗口守卫。
        /// </summary>
        /// <param name="decision">分表策略决策快照。</param>
        /// <returns>
        /// 当且仅当当前生效粒度为 PerDay 时返回 <c>true</c>，保持既有 PerDay 手工预建窗口守卫约束不变。
        /// finer-granularity 规划中的守卫开关将用于后续更细粒度守卫扩展，不影响现有 PerDay 守卫触发。
        /// </returns>
        internal static bool ShouldEnforcePerDayPrebuildGuard(ParcelShardingStrategyDecision decision) {
            return decision.EffectiveDateMode == ExpandByDateMode.PerDay;
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
        /// 从分表注册同源规则动态构建 PerDay 探测实体类型清单。
        /// </summary>
        /// <returns>实体类型数组。</returns>
        private static IReadOnlyList<Type> BuildPerDayShardingEntityTypes() {
            return PersistenceServiceCollectionExtensions.GetParcelPerDayShardingEntityTypes();
        }

        /// <summary>
        /// 分表治理守卫异常：用于区分启动期治理边界失败与数据库连接失败。
        /// </summary>
        private sealed class ShardingGovernanceGuardException : InvalidOperationException {
            /// <summary>
            /// 初始化分表治理守卫异常实例。
            /// </summary>
            /// <param name="message">异常消息。</param>
            public ShardingGovernanceGuardException(string message) : base(message) {
            }
        }
    }
}
