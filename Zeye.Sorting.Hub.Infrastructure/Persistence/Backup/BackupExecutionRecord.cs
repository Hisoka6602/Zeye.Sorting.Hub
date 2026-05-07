namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份治理执行记录。
/// </summary>
public sealed class BackupExecutionRecord {
    /// <summary>
    /// 已禁用状态。
    /// </summary>
    public const string DisabledStatus = "Disabled";

    /// <summary>
    /// 已完成状态。
    /// </summary>
    public const string CompletedStatus = "Completed";

    /// <summary>
    /// 失败状态。
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <summary>
    /// 记录本地时间。
    /// </summary>
    public DateTime RecordedAtLocal { get; init; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否启用备份治理。
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 是否仅执行 dry-run。
    /// </summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 配置层提供器名称。
    /// </summary>
    public required string ConfiguredProviderName { get; init; }

    /// <summary>
    /// 数据库名称。
    /// </summary>
    public required string DatabaseName { get; init; }

    /// <summary>
    /// 摘要说明。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 计划输出的备份文件路径。
    /// </summary>
    public required string PlannedBackupFilePath { get; init; }

    /// <summary>
    /// 备份命令文本。
    /// </summary>
    public required string CommandText { get; init; }

    /// <summary>
    /// 实际校验命中的备份文件路径。
    /// </summary>
    public string? VerifiedBackupFilePath { get; init; }

    /// <summary>
    /// 实际校验命中的备份文件本地时间。
    /// </summary>
    public DateTime? VerifiedBackupAtLocal { get; init; }

    /// <summary>
    /// 备份文件是否存在。
    /// </summary>
    public bool HasBackupFile { get; init; }

    /// <summary>
    /// 备份文件是否在允许年龄内。
    /// </summary>
    public bool IsBackupFileFresh { get; init; }

    /// <summary>
    /// 恢复 Runbook 路径。
    /// </summary>
    public required string RestoreRunbookPath { get; init; }

    /// <summary>
    /// 恢复演练记录路径。
    /// </summary>
    public required string DrillRecordPath { get; init; }

    /// <summary>
    /// 创建禁用记录。
    /// </summary>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="configuredProviderName">配置层提供器名称。</param>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>执行记录。</returns>
    public static BackupExecutionRecord CreateDisabled(string providerName, string configuredProviderName, string databaseName) {
        return new BackupExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DisabledStatus,
            IsEnabled = false,
            IsDryRun = true,
            ProviderName = providerName,
            ConfiguredProviderName = configuredProviderName,
            DatabaseName = databaseName,
            Summary = "备份治理未启用。",
            PlannedBackupFilePath = string.Empty,
            CommandText = string.Empty,
            RestoreRunbookPath = string.Empty,
            DrillRecordPath = string.Empty
        };
    }
}
