namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// SQL Server 备份提供器。
/// </summary>
public sealed class SqlServerBackupProvider : IBackupProvider {
    /// <summary>
    /// 提供器名称。
    /// </summary>
    public string ProviderName => ConfiguredProviderNames.SqlServer;

    /// <summary>
    /// 备份文件扩展名。
    /// </summary>
    public string ArtifactExtension => ".bak";

    /// <summary>
    /// 解析数据库名称。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>数据库名称。</returns>
    public string ResolveDatabaseName(string connectionString) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        return BackupConnectionStringParser.GetRequiredValue(values, "Initial Catalog", "Database");
    }

    /// <summary>
    /// 构建 SQL Server 备份命令。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>备份命令。</returns>
    public string BuildBackupCommand(string connectionString, string backupFilePath) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        var server = BackupConnectionStringParser.GetRequiredValue(values, "Server", "Data Source");
        var database = ResolveDatabaseName(connectionString);
        var user = BackupConnectionStringParser.GetOptionalValue(values, string.Empty, "User Id", "Uid", "UserID");
        if (string.IsNullOrWhiteSpace(user)) {
            return $"sqlcmd -S \"{server}\" -d master -Q \"BACKUP DATABASE [{database}] TO DISK = N'{backupFilePath}' WITH INIT, COMPRESSION, CHECKSUM\"";
        }

        return $"sqlcmd -S \"{server}\" -U \"{user}\" -P \"<PASSWORD>\" -d master -Q \"BACKUP DATABASE [{database}] TO DISK = N'{backupFilePath}' WITH INIT, COMPRESSION, CHECKSUM\"";
    }

    /// <summary>
    /// 构建 SQL Server 恢复 Runbook。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <param name="backupFilePath">备份文件路径。</param>
    /// <returns>恢复 Runbook 文本。</returns>
    public string BuildRestoreRunbook(string connectionString, string backupFilePath) {
        var values = BackupConnectionStringParser.Parse(connectionString);
        var server = BackupConnectionStringParser.GetRequiredValue(values, "Server", "Data Source");
        var database = ResolveDatabaseName(connectionString);
        return $"1. 人工审批恢复窗口并确认目标数据库。\n2. 先执行 sqlcmd -S \"{server}\" -d master -Q \"RESTORE VERIFYONLY FROM DISK = N'{backupFilePath}'\"。\n3. 生产环境仅允许人工执行 RESTORE DATABASE [{database}]，不得自动覆盖现网库。";
    }
}
