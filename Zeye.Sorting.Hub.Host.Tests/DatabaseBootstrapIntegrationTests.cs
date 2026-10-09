using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在已有的独立验收服务器上创建唯一临时库，验证生产环境首次初始化与重复启动。</summary>
public sealed class DatabaseBootstrapIntegrationTests {
    /// <summary>先预演，再执行真实迁移，随后重复启动；不能把新库或已可用库误判为配置错误。</summary>
    [DatabaseIntegrationTheory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    public async Task FreshDatabaseMigratesAndRestartsAfterDryRun(string provider) {
        using var storage = new ConfigurationTestStorage();
        var name = "ZEYE_STARTUP_" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var connection = provider switch {
            "MySql" => $"Server=127.0.0.1;Port=13316;Database={name};User Id=root;Password=MatrixMysql20261007_ForTests;SslMode=Required;Pooling=False;Connection Timeout=5",
            "SqlServer" => $"Server=127.0.0.1,14336;Database={name};User Id=sa;Password=MatrixSqlServer20261007_ForTests;TrustServerCertificate=True;Pooling=False;Connect Timeout=5",
            "Oracle" => $"User Id={name};Password=MatrixApp20261007_ForTests;Data Source=127.0.0.1:15236/FREEPDB1;Pooling=False;Connection Timeout=5",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        const string oracleAdministration = "User Id=SYS;Password=MatrixOracle20261007_ForTests;Data Source=127.0.0.1:15236/FREEPDB1;DBA Privilege=SYSDBA;Pooling=False;Connection Timeout=5";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:OracleAdministration"] = oracleAdministration
        }).Build();
        IDatabaseDialect dialect = provider switch { "MySql" => new MySqlDialect(), "SqlServer" => new SqlServerDialect(), _ => new OracleDialect(configuration) };
        await using var administration = dialect.CreateAdministrationConnection(connection);
        // 只有已验证原本不存在的随机临时库允许在 finally 中清理。
        Assert.False(await dialect.DatabaseExistsAsync(administration, name, CancellationToken.None));
        try {
            for (var startup = 0; startup < 3; startup++) {
                var dryRun = startup == 0;
                await using (var app = await DatabaseSetupTests.CreateInitializedDatabaseHostAsync(storage, provider, connection, dryRun,
                    provider == "Oracle" && startup < 2 ? oracleAdministration : null)) {
                    var state = app.Services.GetRequiredService<DatabaseStartupState>();
                    var record = app.Services.GetRequiredService<MigrationGovernanceStateStore>().GetLatestExecutionRecord();
                    if (dryRun) Assert.True(state.RequiresConfiguration);
                    else {
                        Assert.True(state.Ready, $"{provider}: {record?.Status}; {record?.SkipReason}; {record?.FailureMessage}");
                        await using var db = await app.Services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
                        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
                    }
                }
                if (dryRun && provider != "Oracle") await AssertExistingObjectsRemainProtectedAsync(storage, provider, connection);
            }
            if (provider == "SqlServer") await AssertBusinessOnlySqlServerUserCanStartAsync(storage, connection, (SqlConnection)administration, name);
        }
        finally {
            if (!name.StartsWith("ZEYE_STARTUP_", StringComparison.Ordinal) || name.Length != 25 || !name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                throw new InvalidOperationException("数据库回归清理名称无效。");
            if (administration.State != System.Data.ConnectionState.Open) await administration.OpenAsync();
            await using var command = administration.CreateCommand();
            command.CommandText = provider switch {
                "MySql" => $"DROP DATABASE IF EXISTS `{name}`",
                "SqlServer" => $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END",
                _ => $"BEGIN EXECUTE IMMEDIATE 'DROP USER \"{name}\" CASCADE'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -1918 THEN RAISE; END IF; END;"
            };
            try { await command.ExecuteNonQueryAsync(); }
            catch (Exception exception) { NLog.LogManager.GetCurrentClassLogger().Error(exception, "独立启动回归临时库清理失败，Provider={Provider}, Database={Database}", provider, name); throw; }
        }
    }

    /// <summary>未登记迁移但已有用户表的库不能被视为首次安装，已有记录必须保留。</summary>
    private static async Task AssertExistingObjectsRemainProtectedAsync(ConfigurationTestStorage storage, string provider, string connection) {
        await using DbConnection database = provider == "MySql" ? new MySqlConnection(connection) : new SqlConnection(connection);
        await database.OpenAsync();
        await using var command = database.CreateCommand();
        command.CommandText = "CREATE TABLE ZeyeStartupGuard (Id int NOT NULL); INSERT INTO ZeyeStartupGuard (Id) VALUES (42)";
        await command.ExecuteNonQueryAsync();
        try {
            await using var app = await DatabaseSetupTests.CreateInitializedDatabaseHostAsync(storage, provider, connection, dryRun: false);
            Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().RequiresConfiguration);
            Assert.False(app.Services.GetRequiredService<MigrationGovernanceStateStore>().GetLatestPlan()!.IsInitialDatabase);
            command.CommandText = "SELECT Id FROM ZeyeStartupGuard";
            Assert.Equal(42, Convert.ToInt32(await command.ExecuteScalarAsync()));
        }
        finally { command.CommandText = "DROP TABLE ZeyeStartupGuard"; await command.ExecuteNonQueryAsync(); }
    }

    /// <summary>只存在于临时业务库、无法连接 master 的包含数据库用户，已有库应正常启动。</summary>
    private static async Task AssertBusinessOnlySqlServerUserCanStartAsync(ConfigurationTestStorage storage, string connection, SqlConnection administration, string databaseName) {
        var user = databaseName + "_APP";
        const string password = "StartupLimited20261009_ForTests";
        await using var command = administration.CreateCommand();
        command.CommandText = "SELECT CAST(value_in_use AS int) FROM sys.configurations WHERE name='contained database authentication'";
        var authenticationWasEnabled = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        try {
            // 仅在固定的独立验收服务器临时启用包含用户，并在 finally 恢复原设置。
            if (!authenticationWasEnabled) {
                command.CommandText = "EXEC sp_configure 'contained database authentication', 1; RECONFIGURE;";
                await command.ExecuteNonQueryAsync();
            }
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET CONTAINMENT=PARTIAL; USE [{databaseName}]; CREATE USER [{user}] WITH PASSWORD=N'{password}'; ALTER ROLE db_owner ADD MEMBER [{user}]; USE master;";
            await command.ExecuteNonQueryAsync();
            var options = new SqlConnectionStringBuilder(connection) { UserID = user, Password = password };
            await using (var target = new SqlConnection(options.ConnectionString)) await target.OpenAsync();
            var master = new SqlConnectionStringBuilder(options.ConnectionString) { InitialCatalog = "master" };
            await using (var forbidden = new SqlConnection(master.ConnectionString)) await Assert.ThrowsAsync<SqlException>(() => forbidden.OpenAsync());
            await using var app = await DatabaseSetupTests.CreateInitializedDatabaseHostAsync(storage, "SqlServer", options.ConnectionString, dryRun: false);
            Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().Ready);
        }
        finally {
            if (!authenticationWasEnabled) {
                command.CommandText = $"USE [{databaseName}]; IF DATABASE_PRINCIPAL_ID(N'{user}') IS NOT NULL DROP USER [{user}]; USE master; ALTER DATABASE [{databaseName}] SET CONTAINMENT=NONE; EXEC sp_configure 'contained database authentication', 0; RECONFIGURE;";
                try { await command.ExecuteNonQueryAsync(); }
                catch (Exception exception) { NLog.LogManager.GetCurrentClassLogger().Error(exception, "独立 SQL Server 包含用户设置恢复失败。"); throw; }
            }
        }
    }
}
