using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text.Json.Nodes;
using LiteDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>真实文件存储验证自动初始化、旧配置迁移、热更新及版本隔离。</summary>
public sealed class RuntimeConfigurationTests {
    /// <summary>崩溃后分别确认已提交、未提交及已被后续版本替代的历史，避免误报失败。</summary>
    [Fact]
    public void PendingHistoryIsRecoveredWithoutGuessingNewerRevisions() {
        using var env = new ConfigurationTestStorage();
        var before = ConfigurationDocument.Parse("{\"value\":1}"); var after = ConfigurationDocument.Parse("{\"value\":2}");
        env.History.Prepare("rules-parcel", "1", "2", before, after);
        env.History.Recover(_ => "2"); Assert.Equal("Committed", env.History.Read()[0].Status);
        env.History.Prepare("rules-parcel", "2", "3", before, after);
        env.History.Recover(_ => "2"); Assert.Equal("Failed", env.History.Read()[0].Status);
        env.History.Prepare("rules-parcel", "3", "4", before, after);
        env.History.Recover(_ => "5"); Assert.Equal("Unconfirmed", env.History.Read()[0].Status);
    }

    /// <summary>无需 appsettings 文件即可建库建集合，重启时已有值保持权威。</summary>
    [Fact]
    public void BootstrapCreatesMissingFilesAndKeepsSavedValuesAfterRestart() {
        using var env = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = env.DirectoryPath, EnvironmentName = "Production", Args = [] });
        ConfigurationBootstrapper.Configure(builder);
        using var app = builder.Build();
        var store = app.Services.GetRequiredService<LiteDbConfigurationStore>();
        Assert.True(File.Exists(store.DatabasePath));
        Assert.True(File.Exists(store.History.DatabasePath));
        Assert.Equal("MySql", builder.Configuration["Persistence:Provider"]);
        var provider = app.Services.GetRequiredService<RuntimeConfigurationProvider>();
        provider.Save(provider.Capture().Revision, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":17}}"));
        Assert.Equal("17", builder.Configuration["LogCleanup:RetentionDays"]);
        using var inspection = new LiteDatabase(new ConnectionString { Filename = store.DatabasePath, Connection = ConnectionType.Shared });
        Assert.Contains("configuration_documents", inspection.GetCollectionNames());
        Assert.Contains("configuration_schema", inspection.GetCollectionNames());
        Assert.DoesNotContain("ConfigurationChanges", inspection.GetCollectionNames());
    }

    /// <summary>旧 JSON 含注释、环境覆盖和未知配置时首次导入；后续旧文件不能覆盖数组。</summary>
    [Fact]
    public void LegacyJsonIsImportedOnceAndShorterArraysDoNotLeakOldEntries() {
        using var env = new ConfigurationTestStorage();
        File.WriteAllText(Path.Combine(env.DirectoryPath, "appsettings.json"), "{ // 旧配置\n\"LogCleanup\":{\"RetentionDays\":8},\"WebRequestAuditLog\":{\"ExcludedPathPrefixes\":[\"/old-one\",\"/old-two\"]},\"LegacyExtra\":{\"Flag\":true}}");
        File.WriteAllText(Path.Combine(env.DirectoryPath, "appsettings.Test.json"), "{\"LogCleanup\":{\"RetentionDays\":9}}");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = env.DirectoryPath, EnvironmentName = "Test", Args = [] });
        ConfigurationBootstrapper.Configure(builder);
        using (var app = builder.Build()) {
            var source = app.Services.GetRequiredService<RuntimeConfigurationProvider>();
            Assert.Equal("9", builder.Configuration["LogCleanup:RetentionDays"]);
            Assert.Equal("true", builder.Configuration["LegacyExtra:Flag"]);
            source.Save(source.Capture().Revision, ConfigurationDocument.Parse("{\"WebRequestAuditLog\":{\"ExcludedPathPrefixes\":[\"/new\"]}}"));
            Assert.Equal(new[] { "/new" }, builder.Configuration.GetSection("WebRequestAuditLog:ExcludedPathPrefixes").Get<string[]>());
        }
        File.WriteAllText(Path.Combine(env.DirectoryPath, "appsettings.json"), "{\"LogCleanup\":{\"RetentionDays\":1}}");
        var restarted = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = env.DirectoryPath, EnvironmentName = "Test", Args = [] });
        ConfigurationBootstrapper.Configure(restarted);
        using var second = restarted.Build();
        Assert.Equal("9", restarted.Configuration["LogCleanup:RetentionDays"]);
        Assert.Equal(new[] { "/new" }, restarted.Configuration.GetSection("WebRequestAuditLog:ExcludedPathPrefixes").Get<string[]>());
    }

    /// <summary>热更新通知 options；启动参数只在重启后生效，历史跨服务实例保留。</summary>
    [Fact]
    public void SaveReloadsOptionsButPinsStartupSettingsAndPersistsOriginalHistory() {
        using var env = new ConfigurationTestStorage();
        var services = new ServiceCollection();
        services.Configure<LogCleanupSettings>(env.Configuration.GetSection("LogCleanup"));
        services.Configure<ResourceThresholdsOptions>(env.Configuration.GetSection("ResourceThresholds"));
        services.AddSingleton<IOptions<ResourceThresholdsOptions>, ReloadableOptions<ResourceThresholdsOptions>>();
        using var serviceProvider = services.BuildServiceProvider();
        var logs = serviceProvider.GetRequiredService<IOptionsMonitor<LogCleanupSettings>>();
        var resources = serviceProvider.GetRequiredService<IOptions<ResourceThresholdsOptions>>();
        var changes = 0; using var subscription = logs.OnChange(_ => changes++);
        var oldUrl = env.Configuration["Hosting:Urls"];
        var saved = env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse(
            "{\"LogCleanup\":{\"RetentionDays\":12},\"ResourceThresholds\":{\"MemoryWarningThresholdMB\":2048},\"Hosting\":{\"Urls\":\"http://localhost:9099\"},\"Access\":{\"MachineApiKey\":\"keep-this-secret\"}}"));
        Assert.Equal(12, logs.CurrentValue.RetentionDays); Assert.Equal(2048, resources.Value.MemoryWarningThresholdMB);
        Assert.Equal(1, changes); Assert.Equal(oldUrl, env.Configuration["Hosting:Urls"]);
        Assert.Contains("Hosting:Urls", saved.RestartRequiredKeys);
        Assert.Contains("ResourceThresholds:MemoryWarningThresholdMB", env.Source.Capture().HotReloadKeys);
        Assert.DoesNotContain("ResourceThresholds:MaxConnectionPoolSize", env.Source.Capture().HotReloadKeys);
        var history = Assert.Single(new ConfigurationHistoryStore(env.History.DatabasePath).Read());
        Assert.Equal("Committed", history.Status); Assert.Contains("keep-this-secret", history.AfterJson);
        Assert.Equal("keep-this-secret", env.Source.Capture().Configuration["Access"]!["MachineApiKey"]!.GetValue<string>());
        var restarted = new RuntimeConfigurationProvider(env.Store); restarted.Load();
        Assert.True(restarted.TryGet("Hosting:Urls", out var url)); Assert.Equal("http://localhost:9099", url);
    }

    /// <summary>两份共享连接同时提交同一旧版本，只有一个能耐久保存。</summary>
    [Fact]
    public async Task CompetingProcessesCannotOverwriteTheSameRevision() {
        using var env = new ConfigurationTestStorage();
        using var second = new LiteDbConfigurationStore(env.Store.DatabasePath, new ConfigurationHistoryStore(env.History.DatabasePath), ConfigurationDocument.Defaults(), new());
        var revision = env.Source.Capture().Revision;
        var next = env.Store.ReadRuntime(); next["LogCleanup"]!["RetentionDays"] = 7;
        var other = env.Store.ReadRuntime(); other["LogCleanup"]!["RetentionDays"] = 18;
        var results = await Task.WhenAll(Task.Run(() => env.Store.WriteRuntime(revision, next)), Task.Run(() => second.WriteRuntime(revision, other)));
        Assert.Single(results.Where(x => x));
        Assert.Single(env.History.Read());
        Assert.True(env.Source.TryReload());
        Assert.Contains(env.Configuration["LogCleanup:RetentionDays"], new[] { "7", "18" });
    }

    /// <summary>保存前校验失败与外部无效变更都不会污染当前 options。</summary>
    [Fact]
    public void InvalidChangesKeepTheLastValidConfiguration() {
        using var env = new ConfigurationTestStorage();
        var revision = env.Source.Capture().Revision;
        Assert.Throws<ArgumentException>(() => env.Source.Save(revision, ConfigurationDocument.Parse("{\"ResourceThresholds\":{\"SampleIntervalSeconds\":0}}")));
        Assert.Equal(revision, env.Source.Capture().Revision); Assert.Empty(env.History.Read());
        Assert.Throws<ArgumentException>(() => env.Source.Save(revision, ConfigurationDocument.Parse("{\"Hosting\":{\"EnableHttpsRedirection\":\"true\"}}")));
        Assert.Throws<ArgumentException>(() => env.Source.Save(revision, ConfigurationDocument.Parse("{\"Persistence\":{\"Backup\":{\"PollIntervalMinutes\":0}}}")));
        var invalid = env.Store.ReadRuntime(); invalid["ResourceThresholds"]!["SampleIntervalSeconds"] = 0;
        Assert.True(env.Store.WriteRuntime(revision, invalid));
        Assert.False(env.Source.TryReload()); Assert.NotNull(env.Source.LastReloadError);
        Assert.Equal("60", env.Configuration["ResourceThresholds:SampleIntervalSeconds"]);
    }

    /// <summary>历史写入失败时配置事务回滚，调用者不会得到部分保存结果。</summary>
    [Fact]
    public void HistoryStorageFailureDoesNotCommitConfiguration() {
        using var env = new ConfigurationTestStorage();
        using var connection = new SqliteConnection("Data Source=" + env.History.DatabasePath + ";Pooling=False"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER reject_history BEFORE INSERT ON ConfigurationChanges BEGIN SELECT RAISE(FAIL,'test failure'); END"; command.ExecuteNonQuery();
        var revision = env.Source.Capture().Revision;
        Assert.Throws<SqliteException>(() => env.Source.Save(revision, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":13}}")));
        Assert.Equal(revision, ConfigurationDocument.Revision(env.Store.ReadRuntime())); Assert.Equal("2", env.Configuration["LogCleanup:RetentionDays"]);
    }

    /// <summary>旧版 LiteDB json 快照自动建立结构标记且保留自定义值。</summary>
    [Fact]
    public void OlderLiteDbSnapshotIsMigratedWithoutLosingValues() {
        using var env = new ConfigurationTestStorage();
        var path = Path.Combine(env.DirectoryPath, "old.db");
        using (var legacy = new LiteDatabase(path)) legacy.GetCollection<BsonDocument>("runtime_configuration").Insert(
            new BsonDocument { ["_id"] = "current", ["json"] = "{\"LogCleanup\":{\"RetentionDays\":21}}" });
        using var store = new LiteDbConfigurationStore(path, env.History, ConfigurationDocument.Defaults(), new());
        Assert.Equal(21, store.ReadRuntime()["LogCleanup"]!["RetentionDays"]!.GetValue<int>());
        Assert.NotNull(store.ReadRuntime()["ResourceThresholds"]);
        Assert.True(store.WriteRuntime(ConfigurationDocument.Revision(store.ReadRuntime()), ConfigurationDocument.Merge(store.ReadRuntime(), ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":22}}"))));
    }

    /// <summary>旧关系库只导入配置白名单，保留版本；新配置提交不改写旧库或业务历史。</summary>
    [Fact]
    public async Task RelationalMigrationPreservesRevisionsAndExcludesBusinessDocuments() {
        using var env = new ConfigurationTestStorage();
        await using var relational = new RelationalParcelTestDatabase(); await relational.InitializeAsync();
        await using (var db = await relational.Factory.CreateDbContextAsync()) {
            db.AddRange(new ManagedDocument { Key = "rules-parcel", Json = "[]", Revision = 7, ModifiedAt = DateTime.Now },
                new ManagedDocument { Key = "parcel-cleanup-history:test", Json = "{\"count\":4}", Revision = 1, ModifiedAt = DateTime.Now });
            await db.SaveChangesAsync();
        }
        var migration = new LegacyConfigurationMigrationHostedService(relational.Factory, env.Store);
        await migration.StartAsync(default);
        var service = new ManagedDocumentService(relational.Factory, env.Store);
        Assert.Equal(7, (await service.ReadAsync("rules-parcel", default))!.Revision);
        Assert.Equal(8, (await service.WriteAsync("rules-parcel", "[]", 7, default))!.Revision);
        await migration.StartAsync(default);
        Assert.Equal(8, env.Store.Read("rules-parcel")!.Revision);
        await using var check = await relational.Factory.CreateDbContextAsync();
        Assert.Equal(7, (await check.Set<ManagedDocument>().SingleAsync(x => x.Key == "rules-parcel")).Revision);
        Assert.NotNull(await service.ReadAsync("parcel-cleanup-history:test", default));
        Assert.Throws<ArgumentException>(() => env.Store.Read("parcel-cleanup-history:test"));
    }

    /// <summary>环境覆盖不落入 LiteDB，保存后的原值仍可追踪。</summary>
    [Fact]
    public void OverridesRemainEffectiveAndAreNeverPersistedAsSettings() {
        using var env = new ConfigurationTestStorage();
        var configuration = new ConfigurationBuilder().Add(env.Source).AddInMemoryCollection(new Dictionary<string, string?> { ["LogCleanup:RetentionDays"] = "44" }).Build();
        env.Source.UseOverrides(configuration);
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":14}}"));
        Assert.Equal("44", configuration["LogCleanup:RetentionDays"]);
        Assert.Equal(14, env.Store.ReadRuntime()["LogCleanup"]!["RetentionDays"]!.GetValue<int>());
        Assert.Contains("LogCleanup:RetentionDays", env.Source.Capture().OverriddenKeys);
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":44}}"));
        Assert.Contains("LogCleanup:RetentionDays", env.Source.Capture().OverriddenKeys);
        ((IDisposable)configuration).Dispose();
    }

    /// <summary>ASPNETCORE_URLS 和命令行 urls 使用的框架键也能显示实际覆盖地址。</summary>
    [Fact]
    public void HostingUrlAliasReportsEffectiveValueAndPreservesStoredAddress() {
        using var env = new ConfigurationTestStorage();
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().Add(env.Source).AddInMemoryCollection(
            new Dictionary<string, string?> { ["urls"] = "http://localhost:9091" }).Build();
        env.Source.UseOverrides(configuration);
        var state = env.Source.Capture();
        Assert.Contains("Hosting:Urls", state.OverriddenKeys);
        Assert.Equal("http://localhost:9091", state.EffectiveConfiguration["Hosting:Urls"]!.GetValue<string>());
        env.Source.Save(state.Revision, ConfigurationDocument.Parse("{\"Hosting\":{\"Urls\":\"http://localhost:9092\"}}"));
        Assert.Equal("http://localhost:9091", env.Source.Capture().EffectiveConfiguration["Hosting:Urls"]!.GetValue<string>());
        Assert.Equal("http://localhost:9092", env.Store.ReadRuntime()["Hosting"]!["Urls"]!.GetValue<string>());
    }

    /// <summary>未修改的密钥保持原值，文本修改按原值保存，过期版本不能覆盖配置。</summary>
    [Fact]
    public void CredentialEditsPreserveUnchangedValuesAndRejectStaleWrites() {
        using var env = new ConfigurationTestStorage();
        var old = env.Source.Capture().Revision;
        env.Source.Save(old, ConfigurationDocument.Parse("{\"Access\":{\"MachineApiKey\":\"private-secret\"}}"));
        Assert.Throws<ConfigurationConflictException>(() => env.Source.Save(old, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":11}}")));
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"LogCleanup\":{\"RetentionDays\":11}}"));
        Assert.Equal("private-secret", env.Configuration["Access:MachineApiKey"]);
        var history = env.History.Read()[0];
        Assert.Contains("private-secret", history.BeforeJson); Assert.Contains("private-secret", history.AfterJson);
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"Access\":{\"MachineApiKey\":\"********\"}}"));
        Assert.Equal("********", env.Configuration["Access:MachineApiKey"]);
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"Access\":{\"MachineApiKey\":\"\"}}"));
        Assert.Equal("", env.Configuration["Access:MachineApiKey"]);
    }

    /// <summary>保存值和环境覆盖值均保留原值，返回的可编辑快照不能改变配置源或耐久数据。</summary>
    [Fact]
    public void CaptureIncludesOriginalOverrideValuesWithoutSharingMutableState() {
        using var env = new ConfigurationTestStorage();
        env.Source.Save(env.Source.Capture().Revision, ConfigurationDocument.Parse("{\"Access\":{\"MachineApiKey\":\"stored-machine-key\"}}"));
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().Add(env.Source).AddInMemoryCollection(
            new Dictionary<string, string?> { ["Access:MachineApiKey"] = "environment-machine-key", ["ConnectionStrings:MySql"] = "Server=localhost;Database=qa;User=qa;Password=qa-original-value;" }).Build();
        env.Source.UseOverrides(configuration);
        var snapshot = env.Source.Capture();
        Assert.Equal("stored-machine-key", snapshot.Configuration["Access"]!["MachineApiKey"]!.GetValue<string>());
        Assert.Equal("environment-machine-key", snapshot.EffectiveConfiguration["Access:MachineApiKey"]!.GetValue<string>());
        Assert.Equal(configuration["ConnectionStrings:MySql"], snapshot.EffectiveConfiguration["ConnectionStrings:MySql"]!.GetValue<string>());
        Assert.Contains("Access:MachineApiKey", snapshot.OverriddenKeys);
        snapshot.Configuration["Access"]!["MachineApiKey"] = "caller-change";
        snapshot.EffectiveConfiguration["Access:MachineApiKey"] = "caller-change";
        Assert.Equal("stored-machine-key", env.Source.Capture().Configuration["Access"]!["MachineApiKey"]!.GetValue<string>());
        Assert.Equal("stored-machine-key", env.Store.ReadRuntime()["Access"]!["MachineApiKey"]!.GetValue<string>());
        Assert.Equal("environment-machine-key", configuration["Access:MachineApiKey"]);
    }

    /// <summary>仅超级管理员读取配置及历史原值；匿名、普通角色和仅有管理权限的账号不能读写。</summary>
    [Fact]
    public async Task RuntimeApiReturnsOriginalValuesOnlyToSuperAdministrator() {
        using var env = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(env.Source); builder.Services.AddSingleton(env.History);
        await using var app = builder.Build();
        app.Use(async (context, next) => {
            if (context.Request.Headers["X-Test-Role"] is var role && !string.IsNullOrEmpty(role)) context.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role.ToString()), new Claim("permission", "access.manage") }, "test"));
            await next();
        });
        app.MapRuntimeConfigurationApis(); await app.StartAsync();
        using var client = app.GetTestClient();
        var revision = env.Source.Capture().Revision;
        foreach (var role in new[] { "", "Operator", "Administrator" }) {
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            if (role.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Role", role);
            var expected = role.Length == 0 ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(expected, (await client.GetAsync("/api/operations/configuration/runtime")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/operations/configuration/history")).StatusCode);
            var denied = await client.PutAsJsonAsync("/api/operations/configuration/runtime", new RuntimeConfigurationUpdate(revision,
                ConfigurationDocument.Parse("{\"Access\":{\"BootstrapKey\":\"denied-value\"}}")));
            Assert.Equal(expected, denied.StatusCode);
            Assert.Equal(revision, env.Source.Capture().Revision);
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "SuperAdministrator");
        var saved = await client.PutAsJsonAsync("/api/operations/configuration/runtime", new RuntimeConfigurationUpdate(env.Source.Capture().Revision,
            ConfigurationDocument.Parse("{\"Access\":{\"BootstrapKey\":\"hidden-bootstrap\"},\"LogCleanup\":{\"RetentionDays\":19}}")));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); Assert.Contains("hidden-bootstrap", await saved.Content.ReadAsStringAsync());
        Assert.Equal("19", env.Configuration["LogCleanup:RetentionDays"]);
        var current = await client.GetAsync("/api/operations/configuration/runtime");
        Assert.Contains("hidden-bootstrap", await current.Content.ReadAsStringAsync());
        Assert.True(current.Headers.CacheControl?.NoStore);
        var history = await client.GetAsync("/api/operations/configuration/history");
        Assert.Contains("hidden-bootstrap", await history.Content.ReadAsStringAsync());
        Assert.True(history.Headers.CacheControl?.NoStore);
    }
}
