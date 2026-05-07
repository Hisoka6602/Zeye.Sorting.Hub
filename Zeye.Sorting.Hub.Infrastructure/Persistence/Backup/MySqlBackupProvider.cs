namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// MySQL 备份提供器。
/// </summary>
public sealed class MySqlBackupProvider : IBackupProvider {
    /// <summary>
    /// 提供器名称。
    /// </summary>
    public string ProviderName => ConfiguredProviderNames.MySql;

    /// <summary>
    /// 备份文件扩展名。
    /// </summary>
    public string ArtifactExtension => ".sql";

    /// <summary>
    /// 解析数据库名称。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>数据库名称。</returns>
    public string ResolveDatabaseName(string connectionString) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        return BackupConnectionStringParser.GetRequiredValue(values, "Database", "Initial Catalog");
    }

    /// <summary>
    /// 构建 MySQL 备份命令。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>备份命令。</returns>
    public string BuildBackupCommand(string connectionString, string backupFilePath) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        var server = BackupConnectionStringParser.GetRequiredValue(values, "Server", "Host", "Data Source");
        var port = BackupConnectionStringParser.GetOptionalValue(values, "3306", "Port");
        var database = ResolveDatabaseName(connectionString);
        var user = BackupConnectionStringParser.GetOptionalValue(values, "root", "User Id", "Uid", "UserID");
        return $"mysqldump --single-transaction --routines --host=\"{server}\" --port={port} --user=\"{user}\" --password=\"<PASSWORD>\" --result-file=\"{backupFilePath}\" \"{database}\"";
    }

    /// <summary>
    /// 构建 MySQL 恢复 Runbook。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>恢复 Runbook 文本。</returns>
    public string BuildRestoreRunbook(string connectionString, string backupFilePath) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        var server = BackupConnectionStringParser.GetRequiredValue(values, "Server", "Host", "Data Source");
        var port = BackupConnectionStringParser.GetOptionalValue(values, "3306", "Port");
        var database = ResolveDatabaseName(connectionString);
        var user = BackupConnectionStringParser.GetOptionalValue(values, "root", "User Id", "Uid", "UserID");
        return $"1. 人工确认目标环境允许恢复。\n2. 先执行 mysql --host=\"{server}\" --port={port} --user=\"{user}\" --password=\"<PASSWORD>\" \"{database}\" < \"{backupFilePath}\"。\n3. 恢复后立即执行业务校验与健康探针检查。";
    }
}
