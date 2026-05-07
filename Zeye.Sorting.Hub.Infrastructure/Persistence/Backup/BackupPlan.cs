namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份计划。
/// </summary>
public sealed class BackupPlan {
    /// <summary>
    /// 计划生成时间。
    /// </summary>
    public DateTime GeneratedAtLocal { get; init; }

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
    /// 备份根目录绝对路径。
    /// </summary>
    public required string BackupDirectoryPath { get; init; }

    /// <summary>
    /// 计划输出的备份文件绝对路径。
    /// </summary>
    public required string PlannedBackupFilePath { get; init; }

    /// <summary>
    /// 备份命令文本。
    /// </summary>
    public required string CommandText { get; init; }
}
