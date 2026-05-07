namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份提供器抽象。
/// </summary>
public interface IBackupProvider {
    /// <summary>
    /// 提供器名称。
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 备份文件扩展名。
    /// </summary>
    string ArtifactExtension { get; }

    /// <summary>
    /// 解析数据库名称。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>数据库名称。</returns>
    string ResolveDatabaseName(string connectionString);

    /// <summary>
    /// 构建备份命令。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>备份命令。</returns>
    string BuildBackupCommand(string connectionString, string backupFilePath);

    /// <summary>
    /// 构建恢复 Runbook。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>恢复 Runbook 文本。</returns>
    string BuildRestoreRunbook(string connectionString, string backupFilePath);
}
