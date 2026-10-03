using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Host.Extensions;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证单宿主发布的页面路由、静态文件边界及 API 响应隔离。</summary>
public sealed class BundledWebUiTests : IAsyncLifetime {
    /// <summary>本用例独立创建的临时发布目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "zeye-bundled-web-tests-" + Guid.NewGuid().ToString("N"));
    /// <summary>按生产接线顺序注册静态前端的隔离宿主。</summary>
    private WebApplication _app = null!;

    /// <summary>准备前端文件及真实响应格式的服务端点。</summary>
    public async Task InitializeAsync() {
        Directory.CreateDirectory(Path.Combine(_root, "wwwroot", "assets"));
        await File.WriteAllTextAsync(Path.Combine(_root, "wwwroot", "index.html"), "<!doctype html><html><body>bundled-ui<script src='/assets/main.js'></script></body></html>");
        await File.WriteAllTextAsync(Path.Combine(_root, "wwwroot", "assets", "main.js"), "console.log('bundled');");
        await File.WriteAllTextAsync(Path.Combine(_root, "appsettings.json"), "{\"DeploymentMarker\":\"server-only\"}");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _root });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        _app = builder.Build(); _app.UseBundledWebUi(); _app.UseRouting();
        _app.MapGet("/api/access/session", static () => Results.Ok(new { configured = false }));
        _app.MapGet("/health/live", static () => Results.Ok(new { status = "Healthy" }));
        await _app.StartAsync();
    }

    /// <summary>仅清理本用例创建的随机临时目录。</summary>
    public async Task DisposeAsync() {
        if (_app is not null) await _app.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>根地址及前端深层地址刷新均返回入口页，入口页不能长期缓存。</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/access/login")]
    [InlineData("/data-overview")]
    [InlineData("/parcels/7202610030111")]
    [InlineData("/governance/parcel-cleanup?tab=history")]
    public async Task DeepLinksReturnBundledEntryPage(string path) {
        using var client = _app.GetTestClient(); using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("bundled-ui", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl!.NoCache);
    }

    /// <summary>已有服务端点优先于单页应用回退，不会误返回 HTML。</summary>
    [Theory]
    [InlineData("/api/access/session")]
    [InlineData("/health/live")]
    public async Task ExistingServerRoutesRetainJsonResponses(string path) {
        using var client = _app.GetTestClient(); using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    /// <summary>服务前缀及缺失文件保留 404，配置文件不能作为静态资源公开。</summary>
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/missing")]
    [InlineData("/API/missing")]
    [InlineData("/health/missing")]
    [InlineData("/swagger/missing")]
    [InlineData("/hubs/missing")]
    [InlineData("/assets/missing.js")]
    [InlineData("/appsettings.json")]
    [InlineData("/nlog.config")]
    public async Task MissingServerRoutesAndPrivateFilesDoNotReturnTheSpa(string path) {
        using var client = _app.GetTestClient(); using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("bundled-ui", await response.Content.ReadAsStringAsync());
    }

    /// <summary>静态资源能加载且支持 HEAD；前端地址不能接收写请求。</summary>
    [Fact]
    public async Task StaticResourcesSupportHeadAndPageWritesAreRejected() {
        using var client = _app.GetTestClient(); using var script = await client.GetAsync("/assets/main.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Contains("bundled", await script.Content.ReadAsStringAsync());
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/data-overview"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode); Assert.Equal(string.Empty, await head.Content.ReadAsStringAsync());
        using var write = await client.PostAsync("/data-overview", null);
        Assert.False(write.IsSuccessStatusCode); Assert.DoesNotContain("bundled-ui", await write.Content.ReadAsStringAsync());
    }

    /// <summary>无前端资源的纯 API 发布仍可启动，不注册页面回退。</summary>
    [Fact]
    public async Task ApiOnlyDeploymentDoesNotRequireAFrontend() {
        var root = Path.Combine(_root, "api-only"); Directory.CreateDirectory(root);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        await using var app = builder.Build(); app.UseBundledWebUi(); app.MapGet("/health/live", static () => Results.Ok());
        await app.StartAsync(); using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/data-overview")).StatusCode);
    }
}
