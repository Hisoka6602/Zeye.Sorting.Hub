using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证旧配置自动补齐数据库字段、保留原值和变更历史，而不重新导入其他默认配置。</summary>
public sealed class DatabaseConfigurationUpgradeTests {
    /// <summary>旧快照缺少的新增连接字段。</summary>
    private static readonly string[] AddedConnections = ["Oracle", "OracleAdministration", "OracleReadOnly", "SQLite", "SQLiteReadOnly"];

    /// <summary>已有存储结构版本也能升级，重启不重复写入或新增历史。</summary>
    [Fact]
    public void CurrentSchemaSnapshotIsCompletedOnceAndPreservesExistingSettings() {
        using var env = new ConfigurationTestStorage();
        var path = Path.Combine(env.DirectoryPath, "old-current.db");
        var original = OldSnapshot();
        WriteSnapshot(path, original);
        string revision;
        using (var upgraded = new LiteDbConfigurationStore(path, env.History, ConfigurationDocument.Defaults(), new())) {
            var current = upgraded.ReadRuntime();
            Assert.Equal(9, ((JsonObject)current["ConnectionStrings"]!).Count);
            foreach (var name in AddedConnections) Assert.NotNull(current["ConnectionStrings"]![name]);
            Assert.Equal(original["ConnectionStrings"]!["MySql"]!.GetValue<string>(), current["ConnectionStrings"]!["MySql"]!.GetValue<string>());
            Assert.Equal("", current["ConnectionStrings"]!["MySqlReadOnly"]!.GetValue<string>());
            Assert.Equal("SqlServer", current["Persistence"]!["Provider"]!.GetValue<string>());
            Assert.Empty((JsonArray)current["WebRequestAuditLog"]!["ExcludedPathPrefixes"]!);
            Assert.Null(current["FusionIngestion"]!["Sources"]);
            Assert.False(current["LegacyExtra"]!["Flag"]!.GetValue<bool>());
            var history = Assert.Single(env.History.Read());
            Assert.Equal("Committed", history.Status);
            Assert.Equal(AddedConnections.Select(name => "ConnectionStrings:" + name).Order(StringComparer.OrdinalIgnoreCase), history.ChangedKeys);
            Assert.Equal(ConfigurationDocument.Revision(original), history.PreviousRevision);
            revision = ConfigurationDocument.Revision(current);
            Assert.Equal(revision, history.Revision);
        }
        using var restarted = new LiteDbConfigurationStore(path, env.History, ConfigurationDocument.Defaults(), new());
        Assert.Equal(revision, ConfigurationDocument.Revision(restarted.ReadRuntime()));
        Assert.Single(env.History.Read());
        using var inspection = OpenSnapshot(path);
        Assert.Equal(8, inspection.GetCollection<BsonDocument>("runtime_configuration").FindById("current")["revision"].AsInt32);
    }

    /// <summary>大小写、空字符串和显式 null 不会被默认值覆盖；数据库以外的新增设置不引入。</summary>
    [Fact]
    public void CompletionRespectsEmptyCaseInsensitiveValuesAndDoesNotResetOtherSections() {
        var original = ConfigurationDocument.Parse("""
            {"connectionstrings":{"mysql":null,"oracle":"saved-oracle","sqlite":"","Custom":"keep"},
             "persistence":{"provider":"SQLite","Extension":false},"WebRequestAuditLog":{"ExcludedPathPrefixes":[]}}
            """);
        var before = original.ToJsonString();
        var completed = ConfigurationDocument.CompleteDatabaseDefaults(original, ConfigurationDocument.Defaults());
        var connections = (JsonObject)completed["ConnectionStrings"]!;
        Assert.Null(connections["MySql"]);
        Assert.Equal("saved-oracle", connections["Oracle"]!.GetValue<string>());
        Assert.Equal("", connections["SQLite"]!.GetValue<string>());
        Assert.Equal("keep", connections["Custom"]!.GetValue<string>());
        Assert.Equal(10, connections.Count);
        Assert.Equal("SQLite", completed["Persistence"]!["Provider"]!.GetValue<string>());
        Assert.False(completed["Persistence"]!["Extension"]!.GetValue<bool>());
        Assert.Null(completed["Persistence"]!["Backup"]);
        Assert.Null(completed["Access"]);
        Assert.Empty((JsonArray)completed["WebRequestAuditLog"]!["ExcludedPathPrefixes"]!);
        Assert.Equal(before, original.ToJsonString());
        Assert.Equal(ConfigurationDocument.Revision(completed), ConfigurationDocument.Revision(
            ConfigurationDocument.CompleteDatabaseDefaults(completed, ConfigurationDocument.Defaults())));
    }

    /// <summary>设计时工具可以读取新增字段，但不会写入旧配置、历史或重新导入旧数组。</summary>
    [Fact]
    public void ReadOnlyLoaderCompletesDatabaseFieldsWithoutPersistingChanges() {
        using var env = new ConfigurationTestStorage();
        var path = Path.Combine(env.DirectoryPath, "read-only-old.db");
        var original = OldSnapshot();
        WriteSnapshot(path, original);
        File.WriteAllText(Path.Combine(env.DirectoryPath, "appsettings.json"), """
            {"ConfigurationStorage":{"LiteDbPath":"read-only-old.db"},"WebRequestAuditLog":{"ExcludedPathPrefixes":["/stale"]}}
            """);
        var loaded = ConfigurationReadOnlyLoader.Load(env.DirectoryPath);
        Assert.Equal(9, ((JsonObject)loaded["ConnectionStrings"]!).Count);
        Assert.Empty((JsonArray)loaded["WebRequestAuditLog"]!["ExcludedPathPrefixes"]!);
        using var inspection = OpenSnapshot(path);
        var saved = inspection.GetCollection<BsonDocument>("runtime_configuration").FindById("current");
        Assert.Equal(original.ToJsonString(), saved["json"].AsString);
        Assert.Equal(7, saved["revision"].AsInt32);
        Assert.Empty(env.History.Read());
    }

    /// <summary>补齐后的字段可通过原管理接口保存，环境覆盖和重启语义仍然有效。</summary>
    [Fact]
    public async Task UpgradedConnectionsAreEditableThroughExistingRuntimeApi() {
        using var env = new ConfigurationTestStorage();
        var path = Path.Combine(env.DirectoryPath, "editable-old.db");
        WriteSnapshot(path, OldSnapshot());
        using var store = new LiteDbConfigurationStore(path, env.History, ConfigurationDocument.Defaults(), new());
        var source = new RuntimeConfigurationProvider(store);
        source.Validating += HostConfigurationValidator.Validate;
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().Add(source).AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Oracle"] = "environment-oracle" }).Build();
        source.UseOverrides(configuration);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(source); builder.Services.AddSingleton(env.History);
        await using var app = builder.Build();
        app.Use(async (context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdministrator")], "test"));
            await next();
        });
        app.MapRuntimeConfigurationApis(); await app.StartAsync();
        using var client = app.GetTestClient();
        var previous = source.Capture().Revision;
        var current = await client.GetFromJsonAsync<JsonObject>("/api/operations/configuration/runtime");
        Assert.Equal(9, ((JsonObject)current!["configuration"]!["ConnectionStrings"]!).Count);
        var changes = ConfigurationDocument.Parse("""
            {"ConnectionStrings":{"Oracle":"saved-new-oracle","OracleAdministration":"saved-admin",
             "OracleReadOnly":"saved-reader","SQLite":"Data Source=data/business/new.db;Foreign Keys=True;",
             "SQLiteReadOnly":"Data Source=data/business/new.db;Mode=ReadOnly;"}}
            """);
        var response = await client.PutAsJsonAsync("/api/operations/configuration/runtime", new RuntimeConfigurationUpdate(previous, changes));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("saved-new-oracle", store.ReadRuntime()["ConnectionStrings"]!["Oracle"]!.GetValue<string>());
        Assert.Equal("environment-oracle", configuration["ConnectionStrings:Oracle"]);
        foreach (var name in AddedConnections) Assert.Contains("ConnectionStrings:" + name, source.Capture().RestartRequiredKeys);
        var stale = await client.PutAsJsonAsync("/api/operations/configuration/runtime", new RuntimeConfigurationUpdate(previous, changes));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(2, env.History.Read().Length);
    }

    /// <summary>历史准备失败时，自动升级不会提交部分配置。</summary>
    [Fact]
    public void FailedHistoryWriteRollsBackAutomaticCompletion() {
        using var env = new ConfigurationTestStorage();
        var path = Path.Combine(env.DirectoryPath, "failed-upgrade.db");
        var original = OldSnapshot(); WriteSnapshot(path, original);
        using var connection = new SqliteConnection("Data Source=" + env.History.DatabasePath + ";Pooling=False"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER reject_completion BEFORE INSERT ON ConfigurationChanges BEGIN SELECT RAISE(FAIL,'test failure'); END";
        command.ExecuteNonQuery();
        Assert.Throws<SqliteException>(() => new LiteDbConfigurationStore(path, env.History, ConfigurationDocument.Defaults(), new()));
        using var inspection = OpenSnapshot(path);
        var saved = inspection.GetCollection<BsonDocument>("runtime_configuration").FindById("current");
        Assert.Equal(original.ToJsonString(), saved["json"].AsString);
        Assert.Equal(7, saved["revision"].AsInt32);
    }

    /// <summary>建立与已有容器配置相同结构的旧快照，不使用真实数据库凭据。</summary>
    private static JsonObject OldSnapshot() {
        var original = ConfigurationDocument.Defaults();
        foreach (var name in AddedConnections) ((JsonObject)original["ConnectionStrings"]!).Remove(name);
        original["ConnectionStrings"]!["MySql"] = "Server=saved-host;Database=old;User Id=fixture;Password=fixture;";
        original["Persistence"]!["Provider"] = "SqlServer";
        original["WebRequestAuditLog"]!["ExcludedPathPrefixes"] = new JsonArray();
        ((JsonObject)original["FusionIngestion"]!).Remove("Sources");
        original["LegacyExtra"] = new JsonObject { ["Flag"] = false };
        return original;
    }

    /// <summary>直接构造已有结构版本和保存版本，验证启动补齐而非首次种子导入。</summary>
    private static void WriteSnapshot(string path, JsonObject original) {
        using var database = OpenSnapshot(path);
        database.GetCollection<BsonDocument>("configuration_schema").Insert(new BsonDocument { ["_id"] = "current", ["version"] = LiteDbConfigurationStore.SchemaVersion });
        database.GetCollection<BsonDocument>("runtime_configuration").Insert(new BsonDocument {
            ["_id"] = "current", ["json"] = original.ToJsonString(), ["revision"] = 7, ["updatedAt"] = DateTime.Now
        });
    }

    /// <summary>仅访问各测试独立目录中的配置快照。</summary>
    private static LiteDatabase OpenSnapshot(string path) => new(new ConnectionString { Filename = path, Connection = ConnectionType.Shared });
}
