using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份治理验证服务。
/// </summary>
public sealed class BackupVerificationService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 配置源。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 宿主环境。
    /// </summary>
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// 备份配置监视器。
    /// </summary>
    private readonly IOptionsMonitor<BackupOptions> _optionsMonitor;

    /// <summary>
    /// 备份提供器。
    /// </summary>
    private readonly IBackupProvider _backupProvider;

    /// <summary>
    /// 恢复演练规划器。
    /// </summary>
    private readonly RestoreDrillPlanner _restoreDrillPlanner;

    /// <summary>
    /// 并发访问锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 最近一次备份计划。
    /// </summary>
    private BackupPlan? _latestPlan;

    /// <summary>
    /// 最近一次执行记录。
    /// </summary>
    private BackupExecutionRecord? _latestExecutionRecord;

    /// <summary>
    /// 初始化备份治理验证服务。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <param name="hostEnvironment">宿主环境。</param>
    /// <param name="optionsMonitor">配置监视器。</param>
    /// <param name="backupProvider">备份提供器。</param>
    /// <param name="restoreDrillPlanner">恢复演练规划器。</param>
    public BackupVerificationService(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<BackupOptions> optionsMonitor,
        IBackupProvider backupProvider,
        RestoreDrillPlanner restoreDrillPlanner) {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _backupProvider = backupProvider ?? throw new ArgumentNullException(nameof(backupProvider));
        _restoreDrillPlanner = restoreDrillPlanner ?? throw new ArgumentNullException(nameof(restoreDrillPlanner));
    }

    /// <summary>
    /// 执行一次备份治理验证。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行记录。</returns>
    public Task<BackupExecutionRecord> ExecuteAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var options = _optionsMonitor.CurrentValue;
        var plan = BuildPlan(options);
        SetLatestPlan(plan);
        if (!options.IsEnabled) {
            var disabledRecord = BackupExecutionRecord.CreateDisabled(plan);
            SetLatestExecutionRecord(disabledRecord);
            Logger.Info("备份治理未启用，Provider={Provider}, BackupDirectory={BackupDirectory}", plan.ProviderName, plan.BackupDirectoryPath);
            return Task.FromResult(disabledRecord);
        }

        try {
            var hasRestoreDrillRecord = !string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath);
            var hasRecentBackupArtifact = plan.LatestBackupArtifactTimeLocal.HasValue
                && plan.LatestBackupArtifactTimeLocal.Value >= plan.ExpectedBackupCutoffAtLocal;

            // 步骤 1：优先检查恢复演练记录，确保恢复链路具备可追溯证据。
            if (!hasRestoreDrillRecord) {
                var degradedRecord = BackupExecutionRecord.CreateDegraded(
                    plan,
                    hasRecentBackupArtifact,
                    "未发现恢复演练记录，备份治理降级。",
                    "恢复演练必须记录到 drill-records 目录。");
                SetLatestExecutionRecord(degradedRecord);
                Logger.Warn("备份治理降级：缺少恢复演练记录，RestoreDrillDirectory={RestoreDrillDirectory}", plan.RestoreDrillDirectoryPath);
                return Task.FromResult(degradedRecord);
            }

            // 步骤 2：当前阶段默认 dry-run；若尚未产生真实备份文件，仅记录提示而不阻断服务。
            if (plan.IsDryRun && !hasRecentBackupArtifact) {
                var succeededDryRunRecord = BackupExecutionRecord.CreateSucceeded(
                    plan,
                    hasRecentBackupArtifact: false,
                    "备份治理 dry-run 计划已生成，当前尚无真实备份文件可校验。");
                SetLatestExecutionRecord(succeededDryRunRecord);
                Logger.Info("备份治理 dry-run 完成，ExpectedBackupFile={ExpectedBackupFile}, LatestDrillRecord={LatestDrillRecord}", plan.ExpectedBackupFilePath, plan.LatestRestoreDrillRecordPath);
                return Task.FromResult(succeededDryRunRecord);
            }

            // 步骤 3：若已切换到真实备份阶段，则最近备份文件缺失或过期必须降级告警。
            if (!hasRecentBackupArtifact) {
                var degradedRecord = BackupExecutionRecord.CreateDegraded(
                    plan,
                    hasRecentBackupArtifact: false,
                    "未发现满足时效要求的备份文件，备份治理降级。",
                    "超过预期窗口仍未校验到最近备份文件。");
                SetLatestExecutionRecord(degradedRecord);
                Logger.Warn("备份治理降级：未发现最近备份文件，BackupDirectory={BackupDirectory}, CutoffAtLocal={CutoffAtLocal}", plan.BackupDirectoryPath, plan.ExpectedBackupCutoffAtLocal);
                return Task.FromResult(degradedRecord);
            }

            var succeededRecord = BackupExecutionRecord.CreateSucceeded(
                plan,
                hasRecentBackupArtifact: true,
                "备份治理验证通过。");
            SetLatestExecutionRecord(succeededRecord);
            Logger.Info("备份治理验证通过，LatestBackupArtifact={LatestBackupArtifact}, LatestDrillRecord={LatestDrillRecord}", plan.LatestBackupArtifactPath, plan.LatestRestoreDrillRecordPath);
            return Task.FromResult(succeededRecord);
        }
        catch (Exception exception) {
            var failedRecord = BackupExecutionRecord.CreateFailed(plan, exception.Message);
            SetLatestExecutionRecord(failedRecord);
            Logger.Error(exception, "备份治理验证失败，Provider={Provider}, BackupDirectory={BackupDirectory}", plan.ProviderName, plan.BackupDirectoryPath);
            return Task.FromResult(failedRecord);
        }
    }

    /// <summary>
    /// 读取最近一次备份计划。
    /// </summary>
    /// <returns>备份计划。</returns>
    public BackupPlan? GetLatestPlan() {
        lock (_syncRoot) {
            return _latestPlan;
        }
    }

    /// <summary>
    /// 读取最近一次执行记录。
    /// </summary>
    /// <returns>执行记录。</returns>
    public BackupExecutionRecord? GetLatestExecutionRecord() {
        lock (_syncRoot) {
            return _latestExecutionRecord;
        }
    }

    /// <summary>
    /// 构建备份计划。
    /// </summary>
    /// <param name="options">备份配置。</param>
    /// <returns>备份计划。</returns>
    private BackupPlan BuildPlan(BackupOptions options) {
        // 本地时间语义：用于生成备份文件名、最近备份时效窗口与健康检查输出。
        var now = DateTime.Now;
        var configuredProvider = _configuration["Persistence:Provider"];
        if (!string.Equals(configuredProvider, _backupProvider.ProviderName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"备份提供器与配置 Provider 不匹配：{configuredProvider}。");
        }

        var connectionString = ResolveConnectionString(configuredProvider);
        var backupDirectoryPath = ResolveAbsolutePath(options.BackupDirectory);
        var restoreDrillDirectoryPath = ResolveAbsolutePath(options.RestoreDrillDirectory);
        var databaseName = _backupProvider.ResolveDatabaseName(connectionString);
        var expectedBackupFilePath = Path.Combine(
            backupDirectoryPath,
            $"{options.BackupFileNamePrefix}-{databaseName}-{now:yyyyMMddHHmmss}{_backupProvider.ArtifactExtension}");
        var latestDrillRecordPath = _restoreDrillPlanner.FindLatestDrillRecordPath(restoreDrillDirectoryPath);
        var latestBackupArtifact = FindLatestBackupArtifact(backupDirectoryPath, options.BackupFileNamePrefix);
        return new BackupPlan {
            GeneratedAtLocal = now,
            ProviderName = _backupProvider.ProviderName,
            DatabaseName = databaseName,
            IsEnabled = options.IsEnabled,
            IsDryRun = options.DryRun,
            BackupDirectoryPath = backupDirectoryPath,
            ExpectedBackupFilePath = expectedBackupFilePath,
            BackupCommandText = _backupProvider.BuildBackupCommand(connectionString, expectedBackupFilePath),
            RestoreRunbookText = _restoreDrillPlanner.BuildRestoreRunbook(
                latestDrillRecordPath,
                _backupProvider.BuildRestoreRunbook(connectionString, expectedBackupFilePath)),
            ExpectedBackupCutoffAtLocal = now.AddHours(-options.ExpectedBackupWithinHours),
            RestoreDrillDirectoryPath = restoreDrillDirectoryPath,
            LatestRestoreDrillRecordPath = latestDrillRecordPath,
            LatestBackupArtifactPath = latestBackupArtifact?.FullName,
            LatestBackupArtifactTimeLocal = latestBackupArtifact?.LastWriteTime
        };
    }

    /// <summary>
    /// 查找最近一次备份文件。
    /// </summary>
    /// <param name="backupDirectoryPath">备份目录。</param>
    /// <param name="backupFileNamePrefix">文件名前缀。</param>
    /// <returns>最近一次备份文件信息。</returns>
    private FileInfo? FindLatestBackupArtifact(string backupDirectoryPath, string backupFileNamePrefix) {
        if (!Directory.Exists(backupDirectoryPath)) {
            return null;
        }

        var latestArtifact = default(FileInfo);
        foreach (var filePath in Directory.EnumerateFiles(
                     backupDirectoryPath,
                     $"{backupFileNamePrefix}-*{_backupProvider.ArtifactExtension}",
                     SearchOption.TopDirectoryOnly)) {
            var file = new FileInfo(filePath);
            if (latestArtifact is null || file.LastWriteTime > latestArtifact.LastWriteTime) {
                latestArtifact = file;
            }
        }

        return latestArtifact;
    }

    /// <summary>
    /// 解析连接字符串。
    /// </summary>
    /// <param name="configuredProvider">配置 Provider。</param>
    /// <returns>连接字符串。</returns>
    private string ResolveConnectionString(string? configuredProvider) {
        if (string.IsNullOrWhiteSpace(configuredProvider)) {
            throw new InvalidOperationException("缺少配置：Persistence:Provider。");
        }

        var connectionString = _configuration.GetConnectionString(configuredProvider);
        if (string.IsNullOrWhiteSpace(connectionString)) {
            throw new InvalidOperationException($"缺少连接字符串：ConnectionStrings:{configuredProvider}");
        }

        return connectionString;
    }

    /// <summary>
    /// 解析绝对路径。
    /// </summary>
    /// <param name="path">原始路径。</param>
    /// <returns>绝对路径。</returns>
    private string ResolveAbsolutePath(string path) {
        if (Path.IsPathRooted(path)) {
            return path;
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, path);
    }

    /// <summary>
    /// 写入最近一次备份计划。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    private void SetLatestPlan(BackupPlan plan) {
        lock (_syncRoot) {
            _latestPlan = plan;
        }
    }

    /// <summary>
    /// 写入最近一次执行记录。
    /// </summary>
    /// <param name="record">执行记录。</param>
    private void SetLatestExecutionRecord(BackupExecutionRecord record) {
        lock (_syncRoot) {
            _latestExecutionRecord = record;
        }
    }
}
