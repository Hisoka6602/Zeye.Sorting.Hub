namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份 Provider 抽象。
/// </summary>
public interface IBackupProvider {
    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 配置层提供器名称。
    /// </summary>
    string ConfiguredProviderName { get; }

    /// <summary>
    /// 备份文件扩展名。
    /// </summary>
    string BackupFileExtension { get; }

    /// <summary>
    /// 从连接字符串解析数据库名称。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>数据库名称。</returns>
    string ResolveDatabaseName(string connectionString);

    /// <summary>
    /// 构建备份计划。
    /// </summary>
    /// <param name="options">备份配置。</param>
    /// <param name="backupDirectoryPath">备份目录绝对路径。</param>
    /// <param name="databaseName">数据库名称。</param>
    /// <param name="generatedAtLocal">计划生成时间。</param>
    /// <returns>备份计划。</returns>
    BackupPlan BuildPlan(BackupOptions options, string backupDirectoryPath, string databaseName, DateTime generatedAtLocal);
}
