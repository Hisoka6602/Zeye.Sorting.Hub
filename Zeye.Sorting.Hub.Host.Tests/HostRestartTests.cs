using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Host.Hosting;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Routing;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>隔离验证重启的权限、保存版本、重复请求和本机配置访问码交接，不启动真实进程。</summary>
public sealed class HostRestartTests {
    /// <summary>普通启动轮换访问码；主动重启只交接一次，成功进入业务模式后清除访问码。</summary>
    [Fact]
    public void SetupKeyIsPreservedOnlyForOneExplicitRestart() {
        using var storage = new ConfigurationTestStorage();
        var first = new DatabaseStartupState(storage.Store.DatabasePath); first.RequireConfiguration();
        var original = File.ReadAllText(first.SetupKeyPath);
        var ordinary = new DatabaseStartupState(storage.Store.DatabasePath); ordinary.RequireConfiguration();
        Assert.False(ordinary.Authorize(original));
        var current = File.ReadAllText(ordinary.SetupKeyPath); ordinary.PreserveSetupKeyForRestart();
        var restarted = new DatabaseStartupState(storage.Store.DatabasePath); restarted.RequireConfiguration();
        Assert.NotEqual(ordinary.InstanceId, restarted.InstanceId); Assert.True(restarted.Authorize(current));
        var another = new DatabaseStartupState(storage.Store.DatabasePath); another.RequireConfiguration();
        Assert.False(another.Authorize(current));
        another.PreserveSetupKeyForRestart(); another.MarkReady();
        Assert.False(File.Exists(another.SetupKeyPath));
        Assert.False(File.Exists(Path.ChangeExtension(another.SetupKeyPath, "restart.key")));
    }

    /// <summary>过期的交接文件不会复用旧访问码。</summary>
    [Fact]
    public void ExpiredRestartKeyIsDiscarded() {
        using var storage = new ConfigurationTestStorage();
        var first = new DatabaseStartupState(storage.Store.DatabasePath); first.RequireConfiguration();
        var original = File.ReadAllText(first.SetupKeyPath); first.PreserveSetupKeyForRestart();
        File.SetLastWriteTime(Path.ChangeExtension(first.SetupKeyPath, "restart.key"), DateTime.Now.AddMinutes(-6));
        var restarted = new DatabaseStartupState(storage.Store.DatabasePath); restarted.RequireConfiguration();
        Assert.False(restarted.Authorize(original));
    }

    /// <summary>重启只接受当前保存版本，重复请求不安排新的启动或访问码交接。</summary>
    [Fact]
    public async Task RestartValidatesRevisionAndDeduplicatesRequests() {
        using var storage = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        var state = new DatabaseStartupState(storage.Store.DatabasePath); state.RequireConfiguration();
        using var restart = new HostRestartCoordinator(app.Lifetime, app.Services.GetRequiredService<IHostLifetime>());
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Headers["X-Zeye-Client"] = "web"; context.Response.Body = new MemoryStream();
        await restart.RequestRestart(context, "stale-version", storage.Source, state).ExecuteAsync(context);
        Assert.Equal(409, context.Response.StatusCode); Assert.False(restart.Requested);
        var revision = storage.Source.Capture().Revision;
        context.Response.Body = new MemoryStream();
        await restart.RequestRestart(context, revision, storage.Source, state).ExecuteAsync(context);
        Assert.Equal(202, context.Response.StatusCode); Assert.True(restart.Requested);
        Assert.False(app.Lifetime.ApplicationStopping.IsCancellationRequested);
        context.Response.Body.Position = 0;
        var result = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(state.InstanceId, result.RootElement.GetProperty("instanceId").GetString());
        Assert.DoesNotContain(File.ReadAllText(state.SetupKeyPath), result.RootElement.GetRawText());
        var pending = Path.ChangeExtension(state.SetupKeyPath, "restart.key");
        var timestamp = File.GetLastWriteTime(pending);
        await restart.RequestRestart(context, revision, storage.Source, state).ExecuteAsync(context);
        Assert.Equal(timestamp, File.GetLastWriteTime(pending));
    }

    /// <summary>本机正确访问码可以请求重启，入口关闭后该路径不可用。</summary>
    [Fact]
    public async Task SetupRestartRequiresCurrentKeyAndSavedRevision() {
        using var storage = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var state = new DatabaseStartupState(storage.Store.DatabasePath); state.RequireConfiguration();
        builder.Services.AddSingleton(storage.Source); builder.Services.AddSingleton(state); builder.Services.AddSingleton<HostRestartCoordinator>();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; return next(context); });
        app.UseMiddleware<DatabaseSetupMiddleware>(); await app.StartAsync();
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        client.DefaultRequestHeaders.Add("X-Zeye-Setup-Key", File.ReadAllText(state.SetupKeyPath));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/setup/host/restart", new { revision = "old" })).StatusCode);
        Assert.False(app.Services.GetRequiredService<HostRestartCoordinator>().Requested);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/setup/host/restart", new { revision = storage.Source.Capture().Revision })).StatusCode);
    }

    /// <summary>正常运行时匿名、普通账号和跨域请求不能重启；超级管理员仍需当前保存版本。</summary>
    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("ordinary", 403)]
    [InlineData("cross-origin", 403)]
    [InlineData("missing-client", 403)]
    [InlineData("stale", 409)]
    [InlineData("administrator", 202)]
    public async Task RuntimeRestartRequiresSuperAdministratorAndRequestOrigin(string role, int statusCode) {
        using var storage = new ConfigurationTestStorage();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        var state = new DatabaseStartupState(storage.Store.DatabasePath); state.MarkReady();
        builder.Services.AddSingleton(storage.Source); builder.Services.AddSingleton(storage.History); builder.Services.AddSingleton(state);
        builder.Services.AddSingleton<HostRestartCoordinator>();
        await using var app = builder.Build();
        app.Use((context, next) => {
            if (role != "anonymous") context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role == "ordinary" ? "ordinary" : "SuperAdministrator")], "test"));
            return next(context);
        });
        app.MapRuntimeConfigurationApis(); await app.StartAsync();
        using var client = app.GetTestClient();
        if (role != "missing-client") client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        if (role == "cross-origin") client.DefaultRequestHeaders.Add("Origin", "http://untrusted.example");
        var response = await client.PostAsJsonAsync("/api/operations/configuration/restart", new { revision = role == "stale" ? "old" : storage.Source.Capture().Revision });
        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
        Assert.Equal(statusCode == 202, app.Services.GetRequiredService<HostRestartCoordinator>().Requested);
    }
}
