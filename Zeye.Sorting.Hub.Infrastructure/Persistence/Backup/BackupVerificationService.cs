using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份校验服务。
/// </summary>
public sealed class BackupVerificationService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 备份 Provider。
    /// </summary>
    private readonly IBackupProvider _backupProvider;

    /// <summary>
    /// 备份配置。
    /// </summary>
    private readonly BackupOptions _options;

    /// <summary>
    /// 配置根。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 恢复演练与 Runbook 规划器。
    /// </summary>
    private readonly RestoreDrillPlanner _restoreDrillPlanner;

    /// <summary>
    /// 最近一次执行记录。
    /// </summary>
    private BackupExecutionRecord? _lastExecutionRecord;

    /// <summary>
    /// 初始化备份校验服务。
    /// </summary>
    /// <param name="backupProvider">备份 Provider。</param>
    /// <param name="options">备份配置。</param>
    /// <param name="configuration">配置根。</param>
    /// <param name="restoreDrillPlanner">恢复演练与 Runbook 规划器。</param>
    public BackupVerificationService(
        IBackupProvider backupProvider,
        IOptions<BackupOptions> options,
        IConfiguration configuration,
        RestoreDrillPlanner restoreDrillPlanner) {
        _backupProvider = backupProvider ?? throw new ArgumentNullException(nameof(backupProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _restoreDrillPlanner = restoreDrillPlanner ?? throw new ArgumentNullException(nameof(restoreDrillPlanner));
    }

    /// <summary>
    /// 获取最近一次执行记录。
    /// </summary>
    /// <returns>执行记录。</returns>
    public BackupExecutionRecord? GetLastExecutionRecord() {
        return Volatile.Read(ref _lastExecutionRecord);
    }

    /// <summary>
    /// 执行一轮备份校验。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行记录。</returns>
    public async Task<BackupExecutionRecord> ExecuteAsync(CancellationToken cancellationToken) {
        try {
            // 步骤 1：解析 provider、连接字符串与数据库名，确保计划基于当前真实配置生成。
            var connectionString = ResolveConnectionString();
            var databaseName = _backupProvider.ResolveDatabaseName(connectionString);
            if (!_options.IsEnabled) {
                return PublishRecord(BackupExecutionRecord.CreateDisabled(_backupProvider.ProviderName, _backupProvider.ConfiguredProviderName, databaseName));
            }

            // 步骤 2：生成计划与文档资产，统一产出备份命令、Runbook 与演练记录。
            var generatedAtLocal = DateTime.Now;
            var backupDirectoryPath = ResolveBackupDirectory();
            Directory.CreateDirectory(Path.Combine(backupDirectoryPath, _backupProvider.ConfiguredProviderName));
            var plan = _backupProvider.BuildPlan(_options, backupDirectoryPath, databaseName, generatedAtLocal);
            var (restoreRunbookPath, drillRecordPath) = await _restoreDrillPlanner.WriteArtifactsAsync(plan, _options, cancellationToken);

            // 步骤 3：校验最近一次备份文件是否存在且未超龄，输出统一执行记录供健康检查复用。
            var verifiedBackupFilePath = FindLatestBackupFile(Path.GetDirectoryName(plan.PlannedBackupFilePath)!);
            if (string.IsNullOrWhiteSpace(verifiedBackupFilePath)) {
                return PublishRecord(new BackupExecutionRecord {
                    RecordedAtLocal = DateTime.Now,
                    Status = BackupExecutionRecord.FailedStatus,
                    IsEnabled = true,
                    IsDryRun = _options.DryRun,
                    ProviderName = plan.ProviderName,
                    ConfiguredProviderName = plan.ConfiguredProviderName,
                    DatabaseName = plan.DatabaseName,
                    Summary = "未发现可用备份文件，备份治理降级。",
                    PlannedBackupFilePath = plan.PlannedBackupFilePath,
                    CommandText = plan.CommandText,
                    HasBackupFile = false,
                    IsBackupFileFresh = false,
                    RestoreRunbookPath = restoreRunbookPath,
                    DrillRecordPath = drillRecordPath
                });
            }

            // 步骤 4：文件系统时间统一使用本地时间语义，避免引入 UTC 转换链路。
            var verifiedBackupAtLocal = File.GetLastWriteTime(verifiedBackupFilePath);
            var isFresh = DateTime.Now - verifiedBackupAtLocal <= TimeSpan.FromHours(_options.MaxAllowedBackupAgeHours);
            return PublishRecord(new BackupExecutionRecord {
                RecordedAtLocal = DateTime.Now,
                Status = isFresh ? BackupExecutionRecord.CompletedStatus : BackupExecutionRecord.FailedStatus,
                IsEnabled = true,
                IsDryRun = _options.DryRun,
                ProviderName = plan.ProviderName,
                ConfiguredProviderName = plan.ConfiguredProviderName,
                DatabaseName = plan.DatabaseName,
                Summary = isFresh
                    ? "备份治理校验通过，已发现可用备份文件。"
                    : "最新备份文件已超出允许时间窗口，备份治理降级。",
                PlannedBackupFilePath = plan.PlannedBackupFilePath,
                CommandText = plan.CommandText,
                VerifiedBackupFilePath = verifiedBackupFilePath,
                VerifiedBackupAtLocal = verifiedBackupAtLocal,
                HasBackupFile = true,
                IsBackupFileFresh = isFresh,
                RestoreRunbookPath = restoreRunbookPath,
                DrillRecordPath = drillRecordPath
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Info("备份治理执行收到取消信号。");
            throw;
        }
        catch (Exception exception) {
            Logger.Error(exception, "备份治理执行失败。");
            var failedRecord = new BackupExecutionRecord {
                RecordedAtLocal = DateTime.Now,
                Status = BackupExecutionRecord.FailedStatus,
                IsEnabled = _options.IsEnabled,
                IsDryRun = _options.DryRun,
                ProviderName = _backupProvider.ProviderName,
                ConfiguredProviderName = _backupProvider.ConfiguredProviderName,
                DatabaseName = "Unknown",
                Summary = $"备份治理执行失败：{exception.Message}",
                PlannedBackupFilePath = string.Empty,
                CommandText = string.Empty,
                HasBackupFile = false,
                IsBackupFileFresh = false,
                RestoreRunbookPath = string.Empty,
                DrillRecordPath = string.Empty
            };
            return PublishRecord(failedRecord);
        }
    }

    /// <summary>
    /// 发布执行记录。
    /// </summary>
    /// <param name="record">执行记录。</param>
    /// <returns>执行记录。</returns>
    private BackupExecutionRecord PublishRecord(BackupExecutionRecord record) {
        Volatile.Write(ref _lastExecutionRecord, record);
        if (record.Status == BackupExecutionRecord.FailedStatus) {
            Logger.Warn(
                "备份治理审计：Status={Status}, Provider={Provider}, Database={Database}, HasBackupFile={HasBackupFile}, IsBackupFileFresh={IsBackupFileFresh}, Summary={Summary}",
                record.Status,
                record.ProviderName,
                record.DatabaseName,
                record.HasBackupFile,
                record.IsBackupFileFresh,
                record.Summary);
            return record;
        }

        Logger.Info(
            "备份治理审计：Status={Status}, Provider={Provider}, Database={Database}, HasBackupFile={HasBackupFile}, IsBackupFileFresh={IsBackupFileFresh}, Summary={Summary}",
            record.Status,
            record.ProviderName,
            record.DatabaseName,
            record.HasBackupFile,
            record.IsBackupFileFresh,
            record.Summary);
        return record;
    }

    /// <summary>
    /// 解析当前 Provider 对应连接字符串。
    /// </summary>
    /// <returns>连接字符串。</returns>
    private string ResolveConnectionString() {
        var connectionString = _configuration.GetConnectionString(_backupProvider.ConfiguredProviderName);
        if (string.IsNullOrWhiteSpace(connectionString)) {
            throw new InvalidOperationException($"缺少连接字符串：ConnectionStrings:{_backupProvider.ConfiguredProviderName}");
        }

        return connectionString;
    }

    /// <summary>
    /// 解析备份目录绝对路径。
    /// </summary>
    /// <returns>绝对路径。</returns>
    private string ResolveBackupDirectory() {
        if (Path.IsPathRooted(_options.BackupDirectory)) {
            return _options.BackupDirectory;
        }

        return _restoreDrillPlanner.ResolveDirectory(_options.BackupDirectory);
    }

    /// <summary>
    /// 查找最近一次备份文件。
    /// </summary>
    /// <param name="providerDirectory">Provider 目录。</param>
    /// <returns>文件路径。</returns>
    private string? FindLatestBackupFile(string providerDirectory) {
        if (!Directory.Exists(providerDirectory)) {
            return null;
        }

        return Directory.EnumerateFiles(providerDirectory, $"*{_backupProvider.BackupFileExtension}", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();
    }
}
