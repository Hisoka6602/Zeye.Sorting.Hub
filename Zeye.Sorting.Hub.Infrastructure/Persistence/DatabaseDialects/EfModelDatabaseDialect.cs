using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

/// <summary>新增关系库共用的 Code First 索引生成与批量结构探测；业务 DDL 由 EF 提供器生成。</summary>
public abstract class EfModelDatabaseDialect : IDatabaseDialect, IBatchShardingPhysicalTableProbe {
    /// <summary>运行配置，仅用于构建无拦截器的模型上下文，避免工厂与调优管线循环依赖。</summary>
    protected readonly IConfiguration Configuration;
    /// <summary>SQLite 相对路径的内容根目录。</summary>
    protected readonly string BaseDirectory;
    /// <summary>启动和结构探测的诊断管线，不参与创建只读设计时模型。</summary>
    protected readonly SlowQueryAutoTuningPipeline? Telemetry;
    /// <summary>保存配置与内容根目录。</summary>
    protected EfModelDatabaseDialect(IConfiguration configuration, IHostEnvironment? environment = null, SlowQueryAutoTuningPipeline? telemetry = null) {
        Configuration = configuration; BaseDirectory = environment?.ContentRootPath ?? Directory.GetCurrentDirectory(); Telemetry = telemetry;
    }
    /// <inheritdoc />
    public abstract string ProviderName { get; }
    /// <summary>配置层提供器键。</summary>
    protected abstract string ConfiguredProvider { get; }
    /// <summary>参数化表名查询，只读取数据库元数据。</summary>
    protected abstract string TablesSql { get; }
    /// <summary>参数化索引名查询，只读取数据库元数据。</summary>
    protected abstract string IndexesSql { get; }
    /// <inheritdoc />
    public abstract IReadOnlyList<string> GetOptionalBootstrapSql();
    /// <inheritdoc />
    public abstract IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk);
    /// <inheritdoc />
    public abstract bool ShouldIgnoreAutoTuningException(Exception exception);
    /// <inheritdoc />
    public abstract string ExtractDatabaseName(string connectionString);
    /// <inheritdoc />
    public abstract DbConnection CreateAdministrationConnection(string connectionString);
    /// <inheritdoc />
    public abstract Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken);
    /// <inheritdoc />
    public abstract Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) {
        var columns = DatabaseProviderOperations.NormalizeWhereColumns(whereColumns, 3).Distinct(StringComparer.Ordinal).ToArray();
        if (columns.Length == 0) return [];
        DatabaseIdentifierPolicy.NormalizeDatabaseName(tableName, nameof(tableName));
        foreach (var column in columns) DatabaseIdentifierPolicy.NormalizeDatabaseName(column, nameof(whereColumns));
        if (!string.IsNullOrWhiteSpace(schemaName)) DatabaseIdentifierPolicy.NormalizeDatabaseName(schemaName, nameof(schemaName));
        var options = new DbContextOptionsBuilder<SortingHubDbContext>();
        AdditionalDbContextOptions.Configure(options, ConfiguredProvider, Configuration.GetConnectionString(ConfiguredProvider)!, BaseDirectory);
        using var db = new SortingHubDbContext(options.Options);
        var operation = new CreateIndexOperation {
            Name = DatabaseProviderOperations.BuildIndexName(schemaName, tableName, columns, 60),
            Table = tableName, Schema = schemaName, Columns = columns
        };
        return db.GetService<IMigrationsSqlGenerator>().Generate([operation])
            .Select(command => command.CommandText).Concat(BuildAutonomousMaintenanceSql(schemaName, tableName, true, false)).ToArray();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) =>
        (await ReadNamesAsync(dbContext, TablesSql, schemaName, null, cancellationToken)).Contains(physicalTableName);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FindMissingTablesAsync(DbContext dbContext, string? schemaName, IReadOnlyList<string> physicalTableNames, CancellationToken cancellationToken) {
        if (physicalTableNames.Count == 0) return [];
        var existing = await ReadNamesAsync(dbContext, TablesSql, schemaName, null, cancellationToken);
        return physicalTableNames.Distinct(StringComparer.Ordinal).Where(name => !existing.Contains(name)).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FindMissingIndexesAsync(DbContext dbContext, string? schemaName, string physicalTableName, IReadOnlyList<string> indexNames, CancellationToken cancellationToken) {
        if (indexNames.Count == 0) return [];
        var existing = await ReadNamesAsync(dbContext, IndexesSql, schemaName, physicalTableName, cancellationToken);
        return indexNames.Distinct(StringComparer.Ordinal).Where(name => !existing.Contains(name)).ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(DbContext dbContext, string? schemaName, string baseTableName, CancellationToken cancellationToken) =>
        (await ReadNamesAsync(dbContext, TablesSql, schemaName, null, cancellationToken))
            .Where(name => name.StartsWith(baseTableName + "_", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();

    /// <summary>在当前事务连接上读取元数据，保留调用方连接状态；Oracle 参数按名称绑定。</summary>
    private static async Task<HashSet<string>> ReadNamesAsync(DbContext db, string sql, string? schema, string? table, CancellationToken token) {
        SlowQueryDbOperations.Attach(db);
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(token);
        try {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            if (command is OracleCommand oracle) oracle.BindByName = true;
            foreach (var (key, value) in new[] { ("schema", schema), ("table", table) }) {
                var name = command is OracleCommand ? key + "Name" : key;
                if (!sql.Contains(":" + name, StringComparison.Ordinal) && !sql.Contains("@" + name, StringComparison.Ordinal)) continue;
                var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value is null ? DBNull.Value : value; command.Parameters.Add(parameter);
            }
            var result = new HashSet<string>(StringComparer.Ordinal);
            await using var reader = await SlowQueryDbOperations.ExecuteReaderAsync(command, token);
            while (await reader.ReadAsync(token)) result.Add(reader.GetString(0));
            return result;
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }
}
