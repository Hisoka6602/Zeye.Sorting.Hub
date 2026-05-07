using System.Data.Common;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// SQL Server 备份 Provider。
/// </summary>
public sealed class SqlServerBackupProvider : IBackupProvider {
    /// <inheritdoc />
    public string ProviderName => "SQLServer";

    /// <inheritdoc />
    public string ConfiguredProviderName => ConfiguredProviderNames.SqlServer;

    /// <inheritdoc />
    public string BackupFileExtension => ".bak";

    /// <inheritdoc />
    public string ResolveDatabaseName(string connectionString) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new DbConnectionStringBuilder {
            ConnectionString = connectionString
        };
        if (BackupConnectionStringValueReader.TryGetFirstNonEmptyValue(builder, out var databaseName, "Initial Catalog", "Database")) {
            return databaseName;
        }

        throw new InvalidOperationException("SQL Server 连接字符串缺少 Initial Catalog 或 Database。");
    }

    /// <inheritdoc />
    public BackupPlan BuildPlan(BackupOptions options, string backupDirectoryPath, string databaseName, DateTime generatedAtLocal) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var providerDirectory = Path.Combine(backupDirectoryPath, ConfiguredProviderName);
        var backupFilePath = Path.Combine(
            providerDirectory,
            $"{BackupFileNamePolicy.SanitizeSegment(options.BackupFilePrefix)}-{generatedAtLocal:yyyyMMddHHmmss}-{BackupFileNamePolicy.SanitizeSegment(databaseName)}{BackupFileExtension}");
        var commandText = $"sqlcmd -Q \"BACKUP DATABASE [{databaseName}] TO DISK = N'{backupFilePath}' WITH INIT, COMPRESSION\"";
        return new BackupPlan {
            GeneratedAtLocal = generatedAtLocal,
            IsEnabled = options.IsEnabled,
            IsDryRun = options.DryRun,
            ProviderName = ProviderName,
            ConfiguredProviderName = ConfiguredProviderName,
            DatabaseName = databaseName,
            BackupDirectoryPath = backupDirectoryPath,
            PlannedBackupFilePath = backupFilePath,
            CommandText = commandText
        };
    }
}
