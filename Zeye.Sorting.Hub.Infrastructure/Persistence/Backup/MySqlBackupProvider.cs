using System.Data.Common;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// MySQL 备份 Provider。
/// </summary>
public sealed class MySqlBackupProvider : IBackupProvider {
    /// <inheritdoc />
    public string ProviderName => "MySQL";

    /// <inheritdoc />
    public string ConfiguredProviderName => ConfiguredProviderNames.MySql;

    /// <inheritdoc />
    public string BackupFileExtension => ".sql";

    /// <inheritdoc />
    public string ResolveDatabaseName(string connectionString) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new DbConnectionStringBuilder {
            ConnectionString = connectionString
        };
        if (BackupConnectionStringValueReader.TryGetFirstNonEmptyValue(builder, out var databaseName, "Database", "Initial Catalog")) {
            return databaseName;
        }

        throw new InvalidOperationException("MySQL 连接字符串缺少 Database 或 Initial Catalog。");
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
        var commandText = $"mysqldump --single-transaction --quick --databases {databaseName} > \"{backupFilePath}\"";
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
