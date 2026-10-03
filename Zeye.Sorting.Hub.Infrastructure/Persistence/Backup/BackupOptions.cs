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

    /// <summary>备份或隔离恢复的整体执行预算（分钟）。可填写范围：1~1440。</summary>
    public int OperationTimeoutMinutes { get; set; } = 30;

    /// <summary>单次流式导出文本容量上限（GiB）。可填写范围：1~1024，不限制数据库的业务存储容量。</summary>
    public int MaxExportGiB { get; set; } = 32;

    /// <summary>是否维护本服务生成的完整备份。可填写范围：true/false，真实删除还受 DryRun 保护。</summary>
    public bool ArtifactRetentionEnabled { get; set; } = true;

    /// <summary>完整备份保留窗口（天）。可填写范围：1~3650，最新最低安全份数始终保留。</summary>
    public int ArtifactRetentionDays { get; set; } = 30;

    /// <summary>最低安全保留份数。可填写范围：2~100，且不大于 MaxRetainedArtifacts。</summary>
    public int MinimumRetainedArtifacts { get; set; } = 3;

    /// <summary>完整备份最多保留份数。可填写范围：MinimumRetainedArtifacts~5000。</summary>
    public int MaxRetainedArtifacts { get; set; } = 744;

    /// <summary>完整备份目录目标容量上限（GiB）。可填写范围：1~4096；不足最低安全份数时保留备份并告警。</summary>
    public int MaxRetainedGiB { get; set; } = 32;
}
