namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份治理配置。
/// </summary>
public sealed class BackupOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Backup";

    /// <summary>
    /// 轮询间隔最小分钟数。
    /// </summary>
    public const int MinPollIntervalMinutes = 1;

    /// <summary>
    /// 轮询间隔最大分钟数。
    /// </summary>
    public const int MaxPollIntervalMinutes = 1440;

    /// <summary>
    /// 备份文件最大允许年龄最小小时数。
    /// </summary>
    public const int MinMaxAllowedBackupAgeHours = 1;

    /// <summary>
    /// 备份文件最大允许年龄最大小时数。
    /// </summary>
    public const int MaxMaxAllowedBackupAgeHours = 8760;

    /// <summary>
    /// 是否启用备份治理。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否仅执行 dry-run。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 后台轮询间隔（分钟）。
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 允许备份文件距当前时间的最大小时数。
    /// </summary>
    public int MaxAllowedBackupAgeHours { get; set; } = 24;

    /// <summary>
    /// 备份文件根目录。
    /// </summary>
    public string BackupDirectory { get; set; } = "backup-artifacts";

    /// <summary>
    /// 备份文件名前缀。
    /// </summary>
    public string BackupFilePrefix { get; set; } = "sorting-hub";

    /// <summary>
    /// 恢复 Runbook 输出目录。
    /// </summary>
    public string RestoreRunbookDirectory { get; set; } = "backup-runbooks";

    /// <summary>
    /// 恢复演练记录输出目录。
    /// </summary>
    public string DrillRecordDirectory { get; set; } = "drill-records";
}
