using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Oracle.ManagedDataAccess.Client;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

/// <summary>Oracle PDB 中业务 schema 的自动创建与 EF Code First 结构维护。</summary>
public sealed class OracleDialect : EfModelDatabaseDialect {
    /// <summary>保存业务与管理连接配置，管理连接仅用于启动期创建 schema。</summary>
    public OracleDialect(IConfiguration configuration, IHostEnvironment? environment = null, SlowQueryAutoTuningPipeline? telemetry = null) : base(configuration, environment, telemetry) { }
    /// <inheritdoc />
    public override string ProviderName => "Oracle";
    /// <inheritdoc />
    protected override string ConfiguredProvider => ConfiguredProviderNames.Oracle;
    /// <inheritdoc />
    protected override string TablesSql => "SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER=NVL(:schemaName,SYS_CONTEXT('USERENV','CURRENT_SCHEMA'))";
    /// <inheritdoc />
    protected override string IndexesSql => "SELECT INDEX_NAME FROM ALL_INDEXES WHERE OWNER=NVL(:schemaName,SYS_CONTEXT('USERENV','CURRENT_SCHEMA')) AND TABLE_NAME=:tableName";
    /// <inheritdoc />
    public override IReadOnlyList<string> GetOptionalBootstrapSql() => [];
    /// <inheritdoc />
    public override bool ShouldIgnoreAutoTuningException(Exception exception) => DatabaseProviderOperations.TryGetProviderErrorNumber(exception, out var number) && number is 955 or 1408;
    /// <inheritdoc />
    public override IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) {
        DatabaseIdentifierPolicy.NormalizeDatabaseName(tableName, nameof(tableName));
        var schema = string.IsNullOrWhiteSpace(schemaName) ? "SYS_CONTEXT('USERENV','CURRENT_SCHEMA')" : "'" + DatabaseIdentifierPolicy.NormalizeDatabaseName(schemaName, nameof(schemaName)) + "'";
        return [$"BEGIN DBMS_STATS.GATHER_TABLE_STATS(ownname => {schema}, tabname => '\"{tableName}\"', cascade => TRUE, no_invalidate => TRUE); END;"];
    }
    /// <inheritdoc />
    public override string ExtractDatabaseName(string connectionString) => DatabaseIdentifierPolicy.NormalizeDatabaseName(new OracleConnectionStringBuilder(connectionString).UserID, nameof(connectionString)).ToUpperInvariant();
    /// <inheritdoc />
    public override DbConnection CreateAdministrationConnection(string connectionString) {
        var connection = new OracleConnection(OracleConnectionStringNormalization.Normalize(
            Configuration.GetConnectionString("OracleAdministration") is { Length: > 0 } admin ? admin : connectionString));
        OracleConnectionLivenessInterceptor.ConfigureConnection(connection);
        SlowQueryDbOperations.Attach(connection, Telemetry);
        return connection;
    }
    /// <inheritdoc />
    public override async Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
        await DatabaseConnectionOpenCoordinator.EnsureOpenedAsync(administrationConnection, cancellationToken);
        await using var command = (OracleCommand)administrationConnection.CreateCommand();
        command.BindByName = true; command.CommandText = "SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME=:name";
        command.Parameters.Add(new OracleParameter("name", ExtractSchema(databaseName)));
        if (Convert.ToInt32(await SlowQueryDbOperations.ExecuteScalarAsync(command, cancellationToken)) == 0) return false;
        if (string.IsNullOrWhiteSpace(Configuration.GetConnectionString("OracleAdministration"))) return true;
        // 兼容已创建用户但授权尚未完成的旧部署，补齐授权仍走自动建库隔离器。
        command.CommandText = "SELECT COUNT(*) FROM ALL_TAB_PRIVS WHERE GRANTEE=:name AND TABLE_SCHEMA='SYS' AND TABLE_NAME='DBMS_LOCK' AND PRIVILEGE='EXECUTE'";
        return Convert.ToInt32(await SlowQueryDbOperations.ExecuteScalarAsync(command, cancellationToken)) > 0;
    }
    /// <inheritdoc />
    public override async Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(Configuration.GetConnectionString("OracleAdministration"))) throw new InvalidOperationException("首次自动创建 Oracle 业务用户需要配置 ConnectionStrings:OracleAdministration（目标 PDB 管理连接）。");
        var schema = ExtractSchema(databaseName);
        var password = new OracleConnectionStringBuilder(Configuration.GetConnectionString(ConfiguredProvider)!).Password;
        if (string.IsNullOrWhiteSpace(password) || password.Any(char.IsControl)) throw new InvalidOperationException("Oracle 自动创建用户需要合法的非空密码。");
        await DatabaseConnectionOpenCoordinator.EnsureOpenedAsync(administrationConnection, cancellationToken);
        // Oracle EF 不创建用户；仅 schema 引导和授权使用原生 DDL，业务表、索引与升级均由 EF 生成。
        await using var command = administrationConnection.CreateCommand();
        command.CommandText = $"CREATE USER \"{schema}\" IDENTIFIED BY \"{password.Replace("\"", "\"\"", StringComparison.Ordinal)}\" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS";
        try { await SlowQueryDbOperations.ExecuteNonQueryAsync(command, cancellationToken); }
        catch (OracleException exception) when (exception.Number == 1920) {
            // 此处只验证用户存在，不能把尚未补齐的权限当成用户不存在。
            command.CommandText = "SELECT COUNT(*) FROM ALL_USERS WHERE USERNAME=:name";
            command.Parameters.Add(new OracleParameter("name", schema));
            if (Convert.ToInt32(await SlowQueryDbOperations.ExecuteScalarAsync(command, cancellationToken)) == 0) throw;
            command.Parameters.Clear();
            NLog.LogManager.GetCurrentClassLogger().Warn(exception, "Oracle 业务用户已存在，继续补齐启动授权。");
        }
        command.CommandText = $"GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE TRIGGER TO \"{schema}\"";
        await SlowQueryDbOperations.ExecuteNonQueryAsync(command, cancellationToken);
        command.CommandText = $"GRANT EXECUTE ON SYS.DBMS_LOCK TO \"{schema}\"";
        await SlowQueryDbOperations.ExecuteNonQueryAsync(command, cancellationToken);
    }
    /// <summary>Oracle 未加引号的业务用户名统一规范成大写，阻止标识符注入。</summary>
    private static string ExtractSchema(string name) => DatabaseIdentifierPolicy.NormalizeDatabaseName(name, nameof(name)).ToUpperInvariant();
}
