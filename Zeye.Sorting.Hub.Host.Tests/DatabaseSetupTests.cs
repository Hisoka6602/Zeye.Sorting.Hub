using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证无业务数据库时的配置入口、生命周期隔离、持久化与访问限制。</summary>
public sealed class DatabaseSetupTests {
    /// <summary>首次启动从缺失的目录自动建库并执行真实迁移；再次启动沿用相同文件且不返回配置模式。</summary>
    [Fact]
    public async Task NewSqliteDatabaseCreatesDirectoriesAndSchemaAndSurvivesRestart() {
        using var environment = new ConfigurationTestStorage();
        var databasePath = Path.Combine(environment.DirectoryPath, "data", "business", "sorting-hub.db");
        Assert.False(Directory.Exists(Path.GetDirectoryName(databasePath)));
        for (var startup = 0; startup < 2; startup++) {
            await using var app = await CreateInitializedDatabaseHostAsync(environment, "SQLite", $"Data Source={databasePath};Pooling=False", dryRun: false);
            Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().Ready);
            Assert.True(File.Exists(databasePath));
            using var client = app.GetTestClient();
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/business")).StatusCode);
            await using var db = await app.Services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        }
    }

    /// <summary>允许创建目录不能绕过迁移演练；尚未建表的数据库继续停留在配置模式。</summary>
    [Fact]
    public async Task NewSqliteDatabaseStillHonorsMigrationDryRun() {
        using var environment = new ConfigurationTestStorage();
        var databasePath = Path.Combine(environment.DirectoryPath, "data", "business", "sorting-hub.db");
        await using var app = await CreateInitializedDatabaseHostAsync(environment, "SQLite", $"Data Source={databasePath};Pooling=False", dryRun: true);
        Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().RequiresConfiguration);
        Assert.Contains("仅预演", app.Services.GetRequiredService<DatabaseStartupState>().FailureSummary);
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
        Assert.NotEmpty(await db.Database.GetPendingMigrationsAsync());
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>独立数据库连接失败时网页仍启动，业务工厂不能提前实例化。</summary>
    [Fact]
    public async Task UnavailableDatabaseKeepsConfigurationHostAndDefersBusinessServices() {
        using var environment = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DbContextOptionsBuilder<SortingHubDbContext>().UseSqlite(
            $"Data Source={Path.Combine(environment.DirectoryPath, "missing.db")};Mode=ReadOnly;Pooling=False").Options;
        builder.Services.AddSingleton(environment.Source);
        builder.Services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(new PooledDbContextFactory<SortingHubDbContext>(options));
        builder.Services.AddSingleton<IDatabaseDialect>(new SqliteDialect(environment.Configuration));
        var businessCreations = 0;
        builder.Services.AddSingleton<IHostedService>(_ => {
            businessCreations++;
            return new ConfigurationReloadHostedService(environment.Source, environment.Configuration);
        });
        DatabaseStartupHostedService.Register(builder.Services);
        await using var app = builder.Build();
        ConfigureRoutes(app);
        await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(0, businessCreations);
        Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().RequiresConfiguration);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        var report = await readiness.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", report.GetProperty("entries").GetProperty("database-startup").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/business")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/hubs/sorting/negotiate", null)).StatusCode);
    }

    /// <summary>可连接的独立 SQLite 允许业务服务依序启动，并关闭引导入口。</summary>
    [Fact]
    public async Task AvailableDatabaseStartsBusinessServicesAndDisablesSetup() {
        using var environment = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(environment.Source);
        builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton<IDatabaseDialect>(new SqliteDialect(environment.Configuration));
        var businessCreations = 0;
        builder.Services.AddSingleton<IHostedService>(_ => {
            businessCreations++;
            return new ConfigurationReloadHostedService(environment.Source, environment.Configuration);
        });
        DatabaseStartupHostedService.Register(builder.Services);
        await using var app = builder.Build();
        ConfigureRoutes(app);
        await app.StartAsync();
        using var client = app.GetTestClient();
        Assert.Equal(1, businessCreations);
        Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().Ready);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/business")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/setup/database/runtime")).StatusCode);
    }

    /// <summary>后续业务启动失败时停止已开始的任务，网页继续提供本机配置。</summary>
    [Fact]
    public async Task PartialBusinessStartupIsStoppedBeforeConfigurationMode() {
        using var environment = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(environment.Source); builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton<IDatabaseDialect>(new SqliteDialect(environment.Configuration));
        ConfigurationReloadHostedService? started = null;
        builder.Services.AddSingleton<IHostedService>(_ => started = new ConfigurationReloadHostedService(environment.Source, environment.Configuration));
        builder.Services.AddSingleton<IHostedService>(_ => throw new InvalidOperationException("模拟后续业务启动失败。"));
        DatabaseStartupHostedService.Register(builder.Services);
        await using var app = builder.Build(); ConfigureRoutes(app); await app.StartAsync();
        Assert.NotNull(started); Assert.True(started.ExecuteTask!.IsCompleted);
        Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().RequiresConfiguration);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/business")).StatusCode);
    }

    /// <summary>业务任务运行后发生异常时，仍由宿主执行 StopHost，不遗失原异常监督。</summary>
    [Fact]
    public async Task BackgroundFailureStillStopsTheHost() {
        using var environment = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(environment.Source); builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton<IDatabaseDialect>(new SqliteDialect(environment.Configuration));
        using var invalid = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConfigurationStorage:ReloadIntervalSeconds"] = "invalid" }).Build();
        builder.Services.AddSingleton<IHostedService>(_ => new ConfigurationReloadHostedService(environment.Source, invalid));
        DatabaseStartupHostedService.Register(builder.Services);
        await using var app = builder.Build(); ConfigureRoutes(app);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = app.Lifetime.ApplicationStopping.Register(() => stopped.TrySetResult());
        await app.StartAsync();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(app.Lifetime.ApplicationStopping.IsCancellationRequested);
    }

    /// <summary>访问码持有者可独立保存连接配置和历史，重启前生效值保持固定。</summary>
    [Fact]
    public async Task LocalSetupSavesDatabaseOnlyAndRetainsRevisionAndHistory() {
        using var environment = new ConfigurationTestStorage();
        environment.Source.Save(environment.Source.Capture().Revision, ConfigurationDocument.Parse("{\"Access\":{\"MachineApiKey\":\"private-machine-secret\"}}"));
        var state = new DatabaseStartupState(environment.Store.DatabasePath);
        state.RequireConfiguration();
        await using var app = await CreateConfigurationHostAsync(environment, state);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        client.DefaultRequestHeaders.Add("X-Zeye-Setup-Key", File.ReadAllText(state.SetupKeyPath));
        var snapshot = await client.GetFromJsonAsync<JsonElement>("/api/setup/database/runtime");
        Assert.False(snapshot.GetProperty("configuration").TryGetProperty("Access", out _));
        Assert.DoesNotContain("private-machine-secret", snapshot.GetRawText());
        var revision = snapshot.GetProperty("revision").GetString();
        var response = await client.PutAsJsonAsync("/api/setup/database/runtime", new { revision,
            changes = new { Persistence = new { Provider = "SQLite", MigrationGovernance = new { DryRun = false } },
                ConnectionStrings = new { OracleAdministration = "User Id=setup_admin;Password=setup-only-test;Data Source=localhost:1521/TESTPDB;" } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SQLite", environment.Store.ReadRuntime()["Persistence"]!["Provider"]!.GetValue<string>());
        Assert.False(environment.Store.ReadRuntime()["Persistence"]!["MigrationGovernance"]!["DryRun"]!.GetValue<bool>());
        Assert.Contains("setup_admin", environment.Store.ReadRuntime()["ConnectionStrings"]!["OracleAdministration"]!.GetValue<string>());
        Assert.Equal("MySql", environment.Configuration["Persistence:Provider"]);
        Assert.Equal("private-machine-secret", environment.Configuration["Access:MachineApiKey"]);
        Assert.Equal("Committed", environment.History.Read()[0].Status);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Persistence:Provider", saved.GetProperty("result").GetProperty("restartRequiredKeys").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("ConnectionStrings:OracleAdministration", saved.GetProperty("result").GetProperty("restartRequiredKeys").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/setup/database/runtime", new { revision,
            changes = new { Persistence = new { Provider = "MySql" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/setup/database/runtime", new {
            revision = saved.GetProperty("snapshot").GetProperty("revision").GetString(), changes = new { Access = new { EnforceAuthorization = false } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/setup/database/runtime", new {
            revision = saved.GetProperty("snapshot").GetProperty("revision").GetString(), changes = new { Persistence = new { MigrationGovernance = new { BlockDangerousMigrationInProduction = false } } } })).StatusCode);
        var reloaded = new RuntimeConfigurationProvider(environment.Store); reloaded.Load();
        Assert.True(reloaded.TryGet("Persistence:Provider", out var configured)); Assert.Equal("SQLite", configured);
        state.MarkReady();
        Assert.False(File.Exists(state.SetupKeyPath));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/setup/database/runtime")).StatusCode);
    }

    /// <summary>匿名、远程、跨域、错误主机和伪造代理均不能读写数据库凭据。</summary>
    [Theory]
    [InlineData("missing-key", 401)]
    [InlineData("wrong-key", 401)]
    [InlineData("remote", 403)]
    [InlineData("wrong-host", 403)]
    [InlineData("cross-origin", 403)]
    [InlineData("missing-client", 403)]
    public async Task SetupRejectsUntrustedRequests(string scenario, int statusCode) {
        using var environment = new ConfigurationTestStorage();
        var state = new DatabaseStartupState(environment.Store.DatabasePath); state.RequireConfiguration("连接阶段失败，请检查数据库地址。");
        await using var app = await CreateConfigurationHostAsync(environment, state, scenario == "remote");
        using var client = app.GetTestClient();
        if (scenario != "missing-client") client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        if (scenario != "missing-key") client.DefaultRequestHeaders.Add("X-Zeye-Setup-Key", scenario == "wrong-key" ? new string('0', 64) : File.ReadAllText(state.SetupKeyPath));
        if (scenario == "wrong-host") client.DefaultRequestHeaders.Host = "untrusted.example";
        if (scenario == "cross-origin") client.DefaultRequestHeaders.Add("Origin", "http://untrusted.example");
        if (scenario == "remote") client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
        Assert.Equal((HttpStatusCode)statusCode, (await client.GetAsync("/api/setup/database/runtime")).StatusCode);
        Assert.Equal((HttpStatusCode)statusCode, (await client.PostAsJsonAsync("/api/setup/host/restart", new { revision = environment.Source.Capture().Revision })).StatusCode);
        var startup = await client.GetFromJsonAsync<JsonElement>("/api/setup/status");
        Assert.DoesNotContain(File.ReadAllText(state.SetupKeyPath), startup.GetRawText());
        if (scenario is "remote" or "wrong-host") Assert.True(startup.GetProperty("setupKeyPath").ValueKind == JsonValueKind.Null);
        if (scenario is "remote" or "wrong-host") Assert.True(startup.GetProperty("failureSummary").ValueKind == JsonValueKind.Null);
        else Assert.Equal(state.FailureSummary, startup.GetProperty("failureSummary").GetString());
    }

    /// <summary>建立无需账号数据库的真实 HTTP 测试管线。</summary>
    private static async Task<WebApplication> CreateConfigurationHostAsync(ConfigurationTestStorage environment, DatabaseStartupState state, bool remote = false) {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(environment.Source); builder.Services.AddSingleton(state);
        var app = builder.Build(); ConfigureRoutes(app, remote); await app.StartAsync(); return app;
    }

    /// <summary>按实际启动顺序运行四库迁移治理和初始化，配置与归档文件均限定在测试目录。</summary>
    internal static async Task<WebApplication> CreateInitializedDatabaseHostAsync(ConfigurationTestStorage environment, string provider, string connection, bool dryRun, string? oracleAdministration = null) {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(environment.Configuration).AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider,
            [$"ConnectionStrings:{provider}"] = connection,
            ["ConnectionStrings:OracleAdministration"] = oracleAdministration ?? "",
            ["Persistence:MigrationGovernance:DryRun"] = dryRun.ToString(),
            ["Persistence:MigrationGovernance:ArchiveDirectory"] = Path.Combine(environment.DirectoryPath, "migration-scripts")
        });
        builder.Services.AddSingleton(environment.Source);
        builder.Services.AddSortingHubPersistence(builder.Configuration);
        builder.Services.AddHostedService<MigrationGovernanceHostedService>();
        builder.Services.AddHostedService<DatabaseInitializerHostedService>();
        DatabaseStartupHostedService.Register(builder.Services);
        var app = builder.Build(); ConfigureRoutes(app);
        try { await app.StartAsync(); return app; }
        catch { await app.DisposeAsync(); throw; }
    }

    /// <summary>测试管线固定网络来源，并提供可以证明业务是否被调用的端点。</summary>
    private static void ConfigureRoutes(WebApplication app, bool remote = false) {
        app.Use((context, next) => { context.Connection.RemoteIpAddress = remote ? IPAddress.Parse("192.0.2.10") : IPAddress.Loopback; return next(context); });
        app.UseMiddleware<DatabaseSetupMiddleware>();
        app.MapGet("/health/live", () => "Healthy"); app.MapGet("/health/ready", () => "Healthy");
        app.MapGet("/api/business", () => "Business called");
    }
}
