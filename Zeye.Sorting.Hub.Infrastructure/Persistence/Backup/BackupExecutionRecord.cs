namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份治理执行记录。
/// </summary>
public sealed record class BackupExecutionRecord {
    /// <summary>
    /// Disabled 状态文本。
    /// </summary>
    public const string DisabledStatus = "Disabled";

    /// <summary>
    /// Succeeded 状态文本。
    /// </summary>
    public const string SucceededStatus = "Succeeded";

    /// <summary>
    /// Degraded 状态文本。
    /// </summary>
    public const string DegradedStatus = "Degraded";

    /// <summary>
    /// Failed 状态文本。
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <summary>
    /// 记录时间（本地时间）。
    /// </summary>
    public required DateTime RecordedAtLocal { get; init; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否启用备份治理。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 是否 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 是否存在最近备份文件。
    /// </summary>
    public required bool HasRecentBackupArtifact { get; init; }

    /// <summary>
    /// 是否存在恢复演练记录。
    /// </summary>
    public required bool HasRestoreDrillRecord { get; init; }

    /// <summary>
    /// 摘要。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 失败消息。
    /// </summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// 预期备份文件路径。
    /// </summary>
    public string? ExpectedBackupFilePath { get; init; }

    /// <summary>
    /// 最近备份文件路径。
    /// </summary>
    public string? LatestBackupArtifactPath { get; init; }

    /// <summary>
    /// 最近备份文件时间（本地时间）。
    /// </summary>
    public DateTime? LatestBackupArtifactTimeLocal { get; init; }

    /// <summary>
    /// 最近恢复演练记录路径。
    /// </summary>
    public string? LatestRestoreDrillRecordPath { get; init; }

    /// <summary>
    /// 备份命令文本。
    /// </summary>
    public string? BackupCommandText { get; init; }

    /// <summary>
    /// 创建未启用记录。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <returns>执行记录。</returns>
    public static BackupExecutionRecord CreateDisabled(BackupPlan plan) {
        return new BackupExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DisabledStatus,
            IsEnabled = false,
            IsDryRun = plan.IsDryRun,
            HasRecentBackupArtifact = false,
            HasRestoreDrillRecord = !string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath),
            Summary = "备份治理未启用。",
            ExpectedBackupFilePath = plan.ExpectedBackupFilePath,
            LatestRestoreDrillRecordPath = plan.LatestRestoreDrillRecordPath,
            BackupCommandText = plan.BackupCommandText
        };
    }

    /// <summary>
    /// 创建成功记录。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <param name="hasRecentBackupArtifact">是否存在最近备份文件。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>执行记录。</returns>
    public static BackupExecutionRecord CreateSucceeded(BackupPlan plan, bool hasRecentBackupArtifact, string summary) {
        return new BackupExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            Status = SucceededStatus,
            IsEnabled = true,
            IsDryRun = plan.IsDryRun,
            HasRecentBackupArtifact = hasRecentBackupArtifact,
            HasRestoreDrillRecord = !string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath),
            Summary = summary,
            ExpectedBackupFilePath = plan.ExpectedBackupFilePath,
            LatestBackupArtifactPath = plan.LatestBackupArtifactPath,
            LatestBackupArtifactTimeLocal = plan.LatestBackupArtifactTimeLocal,
            LatestRestoreDrillRecordPath = plan.LatestRestoreDrillRecordPath,
            BackupCommandText = plan.BackupCommandText
        };
    }

    /// <summary>
    /// 创建降级记录。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <param name="hasRecentBackupArtifact">是否存在最近备份文件。</param>
    /// <param name="summary">摘要。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <returns>执行记录。</returns>
    public static BackupExecutionRecord CreateDegraded(BackupPlan plan, bool hasRecentBackupArtifact, string summary, string? failureMessage = null) {
        return new BackupExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DegradedStatus,
            IsEnabled = true,
            IsDryRun = plan.IsDryRun,
            HasRecentBackupArtifact = hasRecentBackupArtifact,
            HasRestoreDrillRecord = !string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath),
            Summary = summary,
            FailureMessage = failureMessage,
            ExpectedBackupFilePath = plan.ExpectedBackupFilePath,
            LatestBackupArtifactPath = plan.LatestBackupArtifactPath,
            LatestBackupArtifactTimeLocal = plan.LatestBackupArtifactTimeLocal,
            LatestRestoreDrillRecordPath = plan.LatestRestoreDrillRecordPath,
            BackupCommandText = plan.BackupCommandText
        };
    }

    /// <summary>
    /// 创建失败记录。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <returns>执行记录。</returns>
    public static BackupExecutionRecord CreateFailed(BackupPlan plan, string failureMessage) {
        return new BackupExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            Status = FailedStatus,
            IsEnabled = plan.IsEnabled,
            IsDryRun = plan.IsDryRun,
            HasRecentBackupArtifact = false,
            HasRestoreDrillRecord = !string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath),
            Summary = "备份治理执行失败。",
            FailureMessage = failureMessage,
            ExpectedBackupFilePath = plan.ExpectedBackupFilePath,
            LatestRestoreDrillRecordPath = plan.LatestRestoreDrillRecordPath,
            BackupCommandText = plan.BackupCommandText
        };
    }
}
