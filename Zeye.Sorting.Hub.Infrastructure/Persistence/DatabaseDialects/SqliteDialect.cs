using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

/// <summary>SQLite 文件初始化、Code First 索引和结构探测适配。</summary>
public sealed class SqliteDialect : EfModelDatabaseDialect {
    /// <summary>保存文件路径解析上下文。</summary>
    public SqliteDialect(IConfiguration configuration, IHostEnvironment? environment = null, SlowQueryAutoTuningPipeline? telemetry = null) : base(configuration, environment, telemetry) { }
    /// <inheritdoc />
    public override string ProviderName => "SQLite";
    /// <inheritdoc />
    protected override string ConfiguredProvider => ConfiguredProviderNames.SQLite;
    /// <inheritdoc />
    protected override string TablesSql => "SELECT name FROM sqlite_master WHERE type='table'";
    /// <inheritdoc />
    protected override string IndexesSql => "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name=@table";
    /// <inheritdoc />
    public override IReadOnlyList<string> GetOptionalBootstrapSql() => ["PRAGMA journal_mode=WAL", "PRAGMA optimize"];
    /// <inheritdoc />
    public override IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => ["PRAGMA optimize"];
    /// <inheritdoc />
    public override bool ShouldIgnoreAutoTuningException(Exception exception) => exception is SqliteException sqlite && sqlite.SqliteErrorCode == 1 && sqlite.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase);
    /// <inheritdoc />
    public override string ExtractDatabaseName(string connectionString) => new SqliteConnectionStringBuilder(AdditionalDbContextOptions.NormalizeSqliteConnectionString(connectionString, BaseDirectory)).DataSource;
    /// <inheritdoc />
    public override DbConnection CreateAdministrationConnection(string connectionString) {
        var connection = new SqliteConnection(AdditionalDbContextOptions.NormalizeSqliteConnectionString(connectionString, BaseDirectory));
        SlowQueryDbOperations.Attach(connection, Telemetry); return connection;
    }
    /// <inheritdoc />
    public override Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(databaseName));
    }
    /// <inheritdoc />
    public override async Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
        Directory.CreateDirectory(Path.GetDirectoryName(databaseName)!);
        await DatabaseConnectionOpenCoordinator.EnsureOpenedAsync(administrationConnection, cancellationToken);
    }
    /// <inheritdoc />
    public override async Task<bool> HasUserObjectsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(databaseName)) return false;
        await DatabaseConnectionOpenCoordinator.EnsureOpenedAsync(administrationConnection, cancellationToken);
        await using var command = administrationConnection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name NOT GLOB 'sqlite_*'";
        return Convert.ToInt64(await SlowQueryDbOperations.ExecuteScalarAsync(command, cancellationToken)) > 0;
    }
}
