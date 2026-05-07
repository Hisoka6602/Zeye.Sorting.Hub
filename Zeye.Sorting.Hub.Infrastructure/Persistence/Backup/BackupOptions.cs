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
    /// 轮询间隔最小值（分钟）。
    /// </summary>
    public const int MinVerificationIntervalMinutes = 1;

    /// <summary>
    /// 轮询间隔最大值（分钟）。
    /// </summary>
    public const int MaxVerificationIntervalMinutes = 1440;

    /// <summary>
    /// 最近备份时效最小值（小时）。
    /// </summary>
    public const int MinExpectedBackupWithinHours = 1;

    /// <summary>
    /// 最近备份时效最大值（小时）。
    /// </summary>
    public const int MaxExpectedBackupWithinHours = 720;

    /// <summary>
    /// 是否启用备份治理。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否仅执行 dry-run。
    /// 可填写范围：true（当前版本强制）。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 备份目录。
    /// 可填写范围：相对内容根目录或绝对路径，不能为空。
    /// </summary>
    public string BackupDirectory { get; set; } = "backup-artifacts";

    /// <summary>
    /// 备份文件名前缀。
    /// 可填写范围：非空字符串，建议使用系统或环境标识。
    /// </summary>
    public string BackupFileNamePrefix { get; set; } = "sorting-hub";

    /// <summary>
    /// 备份治理轮询间隔（分钟）。
    /// 可填写范围：1~1440。
    /// </summary>
    public int VerificationIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 允许的最近备份时效（小时）。
    /// 可填写范围：1~720。
    /// </summary>
    public int ExpectedBackupWithinHours { get; set; } = 24;

    /// <summary>
    /// 恢复演练记录目录。
    /// 可填写范围：相对内容根目录或绝对路径，不能为空。
    /// </summary>
    public string RestoreDrillDirectory { get; set; } = "drill-records";
}
