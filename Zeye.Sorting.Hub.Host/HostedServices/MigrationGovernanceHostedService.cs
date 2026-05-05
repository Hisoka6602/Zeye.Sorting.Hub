using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Hosting;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 迁移治理后台服务。
/// </summary>
public sealed class MigrationGovernanceHostedService : IHostedService {
    /// <summary>
    /// 启用迁移治理配置键。可填写值:true / false。
    /// </summary>
    private const string MigrationGovernanceEnabledConfigKey = "Persistence:MigrationGovernance:IsEnabled";

    /// <summary>
    /// dry-run 配置键。可填写值:true / false。
    /// </summary>
    private const string MigrationGovernanceDryRunConfigKey = "Persistence:MigrationGovernance:DryRun";

    /// <summary>
    /// 迁移脚本归档目录配置键。可填写值:相对路径或绝对路径。
    /// </summary>
    private const string MigrationGovernanceArchiveDirectoryConfigKey = "Persistence:MigrationGovernance:ArchiveDirectory";

    /// <summary>
    /// 生产环境危险迁移阻断配置键。可填写值:true / false。
    /// </summary>
    private const string MigrationGovernanceBlockDangerousConfigKey = "Persistence:MigrationGovernance:BlockDangerousMigrationInProduction";

    /// <summary>
    /// 默认归档目录。
    /// </summary>
    private const string DefaultArchiveDirectory = "migration-scripts";

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// DbContext 工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _dbContextFactory;

    /// <summary>
    /// 数据库方言。
    /// </summary>
    private readonly IDatabaseDialect _dialect;

    /// <summary>
    /// 宿主环境。
    /// </summary>
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// 配置源。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 危险 SQL 识别器。
    /// </summary>
    private readonly MigrationSafetyEvaluator _migrationSafetyEvaluator;

    /// <summary>
    /// 脚本归档服务。
    /// </summary>
    private readonly MigrationScriptArchiveService _migrationScriptArchiveService;

    /// <summary>
    /// 回滚参考脚本生成器。
    /// </summary>
    private readonly MigrationRollbackScriptProvider _migrationRollbackScriptProvider;

    /// <summary>
    /// 运行期状态存储。
    /// </summary>
    private readonly MigrationGovernanceStateStore _migrationGovernanceStateStore;

    /// <summary>
    /// 初始化迁移治理后台服务。
    /// </summary>
    /// <param name="dbContextFactory">DbContext 工厂。</param>
    /// <param name="dialect">数据库方言。</param>
    /// <param name="hostEnvironment">宿主环境。</param>
    /// <param name="configuration">配置源。</param>
    /// <param name="migrationSafetyEvaluator">危险 SQL 识别器。</param>
    /// <param name="migrationScriptArchiveService">脚本归档服务。</param>
    /// <param name="migrationRollbackScriptProvider">回滚参考脚本生成器。</param>
    /// <param name="migrationGovernanceStateStore">运行期状态存储。</param>
    public MigrationGovernanceHostedService(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        IDatabaseDialect dialect,
        IHostEnvironment hostEnvironment,
        IConfiguration configuration,
        MigrationSafetyEvaluator migrationSafetyEvaluator,
        MigrationScriptArchiveService migrationScriptArchiveService,
        MigrationRollbackScriptProvider migrationRollbackScriptProvider,
        MigrationGovernanceStateStore migrationGovernanceStateStore) {
        _dbContextFactory = dbContextFactory;
        _dialect = dialect;
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _migrationSafetyEvaluator = migrationSafetyEvaluator;
        _migrationScriptArchiveService = migrationScriptArchiveService;
        _migrationRollbackScriptProvider = migrationRollbackScriptProvider;
        _migrationGovernanceStateStore = migrationGovernanceStateStore;
    }

    /// <summary>
    /// 启动迁移治理预演。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task StartAsync(CancellationToken cancellationToken) {
        var isEnabled = ResolveIsEnabled(_configuration);
        var isDryRun = ResolveDryRun(_configuration);
        var archiveDirectory = ResolveArchiveDirectory(_configuration);
        var blockDangerousMigrationInProduction = ResolveBlockDangerousMigrationInProduction(_configuration);
        var environmentName = _hostEnvironment.EnvironmentName;
        var providerName = _dialect.ProviderName;
        var isProductionEnvironment = _hostEnvironment.IsProduction();

        try {
            if (!isEnabled) {
                _migrationGovernanceStateStore.SetLatestPlan(null);
                _migrationGovernanceStateStore.SetLatestExecutionRecord(MigrationExecutionRecord.CreateDisabled(providerName, environmentName));
                NLogLogger.Info("迁移治理未启用，Provider={Provider}, Environment={Environment}", providerName, environmentName);
                return;
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var allMigrations = dbContext.Database.GetMigrations().ToArray();
            var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            var forwardScript = GenerateForwardScript(dbContext, appliedMigrations, pendingMigrations);
            var dangerousOperations = _migrationSafetyEvaluator.EvaluateDangerousOperations(forwardScript);
            var (shouldApplyMigrations, skipReason) = EvaluateShouldApplyMigrations(
                pendingMigrations.Length > 0,
                isDryRun,
                isProductionEnvironment,
                blockDangerousMigrationInProduction,
                dangerousOperations);

            string? archivedForwardScriptPath = null;
            string? archivedRollbackScriptPath = null;
            if (pendingMigrations.Length > 0) {
                var migrationName = pendingMigrations[^1];
                archivedForwardScriptPath = await _migrationScriptArchiveService.ArchiveForwardScriptAsync(
                    archiveDirectory,
                    providerName,
                    migrationName,
                    forwardScript,
                    cancellationToken);

                var preArchivePlan = new MigrationPlan {
                    GeneratedAtLocal = DateTime.Now,
                    ProviderName = providerName,
                    EnvironmentName = environmentName,
                    IsEnabled = true,
                    IsDryRun = isDryRun,
                    BlockDangerousMigrationInProduction = blockDangerousMigrationInProduction,
                    IsProductionEnvironment = isProductionEnvironment,
                    AllMigrations = allMigrations,
                    AppliedMigrations = appliedMigrations,
                    PendingMigrations = pendingMigrations,
                    DangerousOperations = dangerousOperations,
                    ShouldApplyMigrations = shouldApplyMigrations,
                    SkipReason = skipReason,
                    ArchivedForwardScriptPath = archivedForwardScriptPath
                };
                var rollbackScript = _migrationRollbackScriptProvider.BuildManualRollbackScript(preArchivePlan);
                archivedRollbackScriptPath = await _migrationScriptArchiveService.ArchiveRollbackScriptAsync(
                    archiveDirectory,
                    providerName,
                    migrationName,
                    rollbackScript,
                    cancellationToken);
            }

            var migrationPlan = new MigrationPlan {
                GeneratedAtLocal = DateTime.Now,
                ProviderName = providerName,
                EnvironmentName = environmentName,
                IsEnabled = true,
                IsDryRun = isDryRun,
                BlockDangerousMigrationInProduction = blockDangerousMigrationInProduction,
                IsProductionEnvironment = isProductionEnvironment,
                AllMigrations = allMigrations,
                AppliedMigrations = appliedMigrations,
                PendingMigrations = pendingMigrations,
                DangerousOperations = dangerousOperations,
                ShouldApplyMigrations = shouldApplyMigrations,
                SkipReason = skipReason,
                ArchivedForwardScriptPath = archivedForwardScriptPath,
                ArchivedRollbackScriptPath = archivedRollbackScriptPath
            };
            _migrationGovernanceStateStore.SetLatestPlan(migrationPlan);

            if (pendingMigrations.Length == 0) {
                _migrationGovernanceStateStore.SetLatestExecutionRecord(MigrationExecutionRecord.CreateSucceeded(migrationPlan, "未发现待执行迁移。"));
                NLogLogger.Info("迁移治理预演完成，未发现待执行迁移，Provider={Provider}, Environment={Environment}", providerName, environmentName);
                return;
            }

            if (!shouldApplyMigrations) {
                _migrationGovernanceStateStore.SetLatestExecutionRecord(MigrationExecutionRecord.CreateSkipped(migrationPlan, skipReason ?? "迁移治理已阻断真实迁移。"));
                NLogLogger.Warn(
                    "迁移治理已阻断真实迁移，Provider={Provider}, Environment={Environment}, PendingCount={PendingCount}, SkipReason={SkipReason}",
                    providerName,
                    environmentName,
                    pendingMigrations.Length,
                    skipReason);
                return;
            }

            _migrationGovernanceStateStore.SetLatestExecutionRecord(MigrationExecutionRecord.CreatePrepared(migrationPlan));
            if (dangerousOperations.Count > 0) {
                NLogLogger.Warn(
                    "迁移治理检测到危险 SQL，但当前环境允许继续执行迁移，Provider={Provider}, Environment={Environment}, DangerousOperations={DangerousOperations}",
                    providerName,
                    environmentName,
                    string.Join(" | ", dangerousOperations));
            }

            NLogLogger.Info(
                "迁移治理预演完成，Provider={Provider}, Environment={Environment}, PendingCount={PendingCount}, ArchivedForwardScript={ArchivedForwardScript}, ArchivedRollbackScript={ArchivedRollbackScript}",
                providerName,
                environmentName,
                pendingMigrations.Length,
                archivedForwardScriptPath ?? "未归档",
                archivedRollbackScriptPath ?? "未归档");
        }
        catch (Exception ex) {
            _migrationGovernanceStateStore.SetLatestPlan(null);
            _migrationGovernanceStateStore.SetLatestExecutionRecord(MigrationExecutionRecord.CreateFailed(
                plan: null,
                failureMessage: ex.Message,
                summary: "迁移治理预演失败，已阻断后续自动迁移。"));
            NLogLogger.Error(ex, "迁移治理预演失败，Provider={Provider}, Environment={Environment}", providerName, environmentName);
        }
    }

    /// <summary>
    /// 停止迁移治理后台服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 评估是否允许执行真实迁移。
    /// </summary>
    /// <param name="hasPendingMigrations">是否存在待执行迁移。</param>
    /// <param name="isDryRun">是否为 dry-run。</param>
    /// <param name="isProductionEnvironment">是否生产环境。</param>
    /// <param name="blockDangerousMigrationInProduction">生产环境是否阻断危险迁移。</param>
    /// <param name="dangerousOperations">危险操作列表。</param>
    /// <returns>是否允许执行与阻断原因。</returns>
    internal static (bool ShouldApplyMigrations, string? SkipReason) EvaluateShouldApplyMigrations(
        bool hasPendingMigrations,
        bool isDryRun,
        bool isProductionEnvironment,
        bool blockDangerousMigrationInProduction,
        IReadOnlyCollection<string> dangerousOperations) {
        if (!hasPendingMigrations) {
            return (true, null);
        }

        if (isDryRun) {
            return (false, "当前处于 dry-run 模式，待执行迁移仅归档不执行。");
        }

        if (isProductionEnvironment && blockDangerousMigrationInProduction && dangerousOperations.Count > 0) {
            return (false, "生产环境检测到危险迁移，已阻断自动执行。");
        }

        return (true, null);
    }

    /// <summary>
    /// 解析是否启用迁移治理。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <returns>解析结果。</returns>
    internal static bool ResolveIsEnabled(IConfiguration configuration) {
        return configuration.GetValue(MigrationGovernanceEnabledConfigKey, true);
    }

    /// <summary>
    /// 解析 dry-run 开关。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <returns>解析结果。</returns>
    internal static bool ResolveDryRun(IConfiguration configuration) {
        return configuration.GetValue(MigrationGovernanceDryRunConfigKey, true);
    }

    /// <summary>
    /// 解析归档目录。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <returns>归档目录。</returns>
    internal static string ResolveArchiveDirectory(IConfiguration configuration) {
        var archiveDirectory = configuration[MigrationGovernanceArchiveDirectoryConfigKey];
        return string.IsNullOrWhiteSpace(archiveDirectory) ? DefaultArchiveDirectory : archiveDirectory.Trim();
    }

    /// <summary>
    /// 解析生产环境危险迁移阻断开关。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <returns>解析结果。</returns>
    internal static bool ResolveBlockDangerousMigrationInProduction(IConfiguration configuration) {
        return configuration.GetValue(MigrationGovernanceBlockDangerousConfigKey, true);
    }

    /// <summary>
    /// 生成待执行迁移的正向脚本。
    /// </summary>
    /// <param name="dbContext">DbContext。</param>
    /// <param name="appliedMigrations">已应用迁移。</param>
    /// <param name="pendingMigrations">待执行迁移。</param>
    /// <returns>脚本文本。</returns>
    private static string GenerateForwardScript(
        SortingHubDbContext dbContext,
        IReadOnlyList<string> appliedMigrations,
        IReadOnlyList<string> pendingMigrations) {
        if (pendingMigrations.Count == 0) {
            return string.Empty;
        }

        var migrator = dbContext.GetService<IMigrator>();
        var fromMigration = appliedMigrations.Count == 0 ? null : appliedMigrations[^1];
        var toMigration = pendingMigrations[^1];
        return migrator.GenerateScript(fromMigration, toMigration);
    }
}
