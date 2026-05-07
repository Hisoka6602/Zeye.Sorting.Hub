namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份治理计划。
/// </summary>
public sealed record class BackupPlan {
    /// <summary>
    /// 计划生成时间（本地时间）。
    /// </summary>
    public required DateTime GeneratedAtLocal { get; init; }

    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 数据库名称。
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// 是否启用备份治理。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 备份目录绝对路径。
    /// </summary>
    public required string BackupDirectoryPath { get; init; }

    /// <summary>
    /// 预期备份文件绝对路径。
    /// </summary>
    public required string ExpectedBackupFilePath { get; init; }

    /// <summary>
    /// 备份命令文本。
    /// </summary>
    public required string BackupCommandText { get; init; }

    /// <summary>
    /// 恢复 Runbook 文本。
    /// </summary>
    public required string RestoreRunbookText { get; init; }

    /// <summary>
    /// 最近备份应不早于该时间（本地时间）。
    /// </summary>
    public required DateTime ExpectedBackupCutoffAtLocal { get; init; }

    /// <summary>
    /// 演练记录目录绝对路径。
    /// </summary>
    public required string RestoreDrillDirectoryPath { get; init; }

    /// <summary>
    /// 最近一次演练记录绝对路径。
    /// </summary>
    public string? LatestRestoreDrillRecordPath { get; init; }

    /// <summary>
    /// 最近一次备份文件绝对路径。
    /// </summary>
    public string? LatestBackupArtifactPath { get; init; }

    /// <summary>
    /// 最近一次备份文件时间（本地时间）。
    /// </summary>
    public DateTime? LatestBackupArtifactTimeLocal { get; init; }
}
