using System.Data.Common;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

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
            return DatabaseIdentifierPolicy.NormalizeDatabaseName(databaseName, nameof(connectionString));
        }

        throw new InvalidOperationException("SQL Server 连接字符串缺少 Initial Catalog 或 Database。");
    }

    /// <inheritdoc />
    public BackupPlan BuildPlan(BackupOptions options, string backupDirectoryPath, string databaseName, DateTime generatedAtLocal) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        var normalizedDatabaseName = DatabaseIdentifierPolicy.NormalizeDatabaseName(databaseName, nameof(databaseName));
        var providerDirectory = Path.Combine(backupDirectoryPath, ConfiguredProviderName);
        var backupFilePath = Path.Combine(
            providerDirectory,
            $"{BackupFileNamePolicy.SanitizeSegment(options.BackupFilePrefix)}-{generatedAtLocal:yyyyMMddHHmmss}-{BackupFileNamePolicy.SanitizeSegment(normalizedDatabaseName)}{BackupFileExtension}");
        var escapedDatabaseName = DatabaseIdentifierPolicy.EscapeSqlServerIdentifier(normalizedDatabaseName);
        var escapedBackupFilePath = BackupCommandTextFormatter.EscapeSqlServerStringLiteral(backupFilePath);
        var backupSql = $"BACKUP DATABASE [{escapedDatabaseName}] TO DISK = N'{escapedBackupFilePath}' WITH INIT, COMPRESSION";
        var commandText = $"sqlcmd -Q {BackupCommandTextFormatter.QuoteDoubleQuotedShellArgument(backupSql)}";
        return new BackupPlan {
            GeneratedAtLocal = generatedAtLocal,
            IsEnabled = options.IsEnabled,
            IsDryRun = options.DryRun,
            ProviderName = ProviderName,
            ConfiguredProviderName = ConfiguredProviderName,
            DatabaseName = normalizedDatabaseName,
            BackupDirectoryPath = backupDirectoryPath,
            PlannedBackupFilePath = backupFilePath,
            CommandText = commandText
        };
    }
}
