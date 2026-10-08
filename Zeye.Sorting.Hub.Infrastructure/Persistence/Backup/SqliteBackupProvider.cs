using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>SQLite 在线一致性备份运行手册，使用 SQLite backup API 而非复制活动中的 WAL 文件。</summary>
public sealed class SqliteBackupProvider(IHostEnvironment? environment = null) : AdditionalDatabaseBackupProvider {
    /// <inheritdoc />
    public override string ProviderName => "SQLite";
    /// <inheritdoc />
    public override string ConfiguredProviderName => ConfiguredProviderNames.SQLite;
    /// <inheritdoc />
    public override string BackupFileExtension => ".db";
    /// <inheritdoc />
    public override string ResolveDatabaseName(string connectionString) => new SqliteConnectionStringBuilder(AdditionalDbContextOptions.NormalizeSqliteConnectionString(connectionString, environment?.ContentRootPath)).DataSource;
    /// <inheritdoc />
    protected override string BuildCommand(string databaseName, string filePath) => $"sqlite3 {BackupCommandTextFormatter.QuotePosixShellArgument(databaseName)} {BackupCommandTextFormatter.QuotePosixShellArgument(".backup '" + filePath.Replace("'", "''", StringComparison.Ordinal) + "'")}";
}
