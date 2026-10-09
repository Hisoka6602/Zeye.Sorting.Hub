using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Models.Realtime;
using Zeye.Sorting.Hub.Host.Extensions;
using Zeye.Sorting.Hub.Host.Hubs;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>通过官方 SignalR 客户端验证生产认证、完整协议方法和受保护的读取路径。</summary>
public sealed class FusionApiTests {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedSettingsReadParcelsReadValues = new[] { "settings.read", "parcels.read" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedParcelsReadSettingsReadValues = new[] { "parcels.read", "settings.read" };

    /// <summary>真实映射生成的账号、规则、Fusion 路由与两条 SignalR 隐式端点均包含中文摘要和业务说明。</summary>
    [Fact]
    public async Task BusinessEndpointsAndSignalRTransportsExposeChineseDescriptions() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await AccessApiTests.CreateAsync(env.Database, configureServices: builder => {
            builder.Services.AddSortingRealtime();
            builder.Services.Configure<Zeye.Sorting.Hub.Host.Middleware.WebRequestAuditLogOptions>(options => options.Enabled = false);
            builder.Services.AddSingleton(new Zeye.Sorting.Hub.Host.Middleware.WebRequestAuditBuffer(32, TimeSpan.FromSeconds(30)));
            builder.Services.AddSingleton<IFusionIngestionGateway>(env.Ingress);
        }, configureRoutes: app => { app.MapFusionIngestion(); app.MapSortingRealtime(); });
        // 只检查此测试宿主实际安装的生产入口，排除账号测试所需的模拟业务响应。
        var productionPrefixes = new[] { "/hubs/", "/api/access", "/api/operations/rules/", "/api/operations/configuration/fusion",
            "/api/parcels/fusion/", "/api/diagnostics/fusion/" };
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } path
                && productionPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
        Assert.True(endpoints.Length >= 19, "未完整读取生产业务端点。");
        foreach (var endpoint in endpoints) {
            Assert.True(endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>() is not null, $"{endpoint.RoutePattern.RawText} 缺少摘要元数据。");
            Assert.True(endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>() is not null, $"{endpoint.RoutePattern.RawText} 缺少业务说明元数据。");
            Assert.Matches("[\\u4e00-\\u9fff]", endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary ?? string.Empty);
            Assert.Matches("[\\u4e00-\\u9fff]", endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description ?? string.Empty);
        }
        foreach (var path in new[] { "/hubs/fusion-ingestion", "/hubs/sorting" }) {
            Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == path);
            Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == path + "/negotiate");
        }
    }

    /// <summary>两种真实传输均验证六方法、超过浏览器预算的报文、断线续传和持久读取。</summary>
    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.LongPolling)]
    public async Task OfficialClientSupportsAllMethodsAndDurableResume(HttpTransportType transport) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env);
        await using var first = Connection(app, transport); await first.StartAsync();
        var registration = await first.InvokeAsync<FusionRegistration>("RegisterFusion", FusionIngressTestEnvironment.Hello());
        var record = FusionIngressTestEnvironment.Fact("parcel.detected", 9007199254740993,
            data: new { barcode = "WIRE-PARCEL", hasSorterDetection = true, detail = new string('x', 40000) });
        var batch = FusionIngressTestEnvironment.Batch(registration, record);
        var receipt = await first.InvokeAsync<HubBatchReceipt>("PublishFacts", batch);
        Assert.Equal("stored", Assert.Single(receipt.Records).Status);
        Assert.Equal(record.BodyJson, Assert.Single(await env.NewIngress().GetFactsAsync(registration.SourceInstanceId, null, 20, default)).BodyJson);
        Assert.Equal("duplicate", Assert.Single((await first.InvokeAsync<HubBatchReceipt>("PublishFacts", batch)).Records).Status);
        var heartbeat = new FusionHeartbeat(registration.SourceInstanceId, registration.JournalId, registration.LeaseId,
            DateTimeOffset.Now.ToOffset(TimeSpan.Zero), 2, 1, 1, 3, 2, true, 1234);
        Assert.Equal("sorting-hub", (await first.InvokeAsync<HubHeartbeatReceipt>("Heartbeat", heartbeat)).HubId);
        await Assert.ThrowsAsync<HubException>(() => first.InvokeAsync<HubBatchReceipt>("PublishFacts", batch with { SourceInstanceId = "fusion-line-02" }));
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/s0AAAAASUVORK5CYII=");
        var descriptor = new HubImageDescriptor(registration.SourceInstanceId, registration.JournalId, registration.LeaseId,
            "wire-image", "wire.png", "image/png", bytes.Length, FusionProtocol.Hash(bytes));
        var begin = await first.InvokeAsync<HubImageBeginReceipt>("BeginImageUpload", descriptor);
        var chunk = new HubImageChunk(begin.UploadId, 0, Convert.ToBase64String(bytes.AsSpan(0, 20)), FusionProtocol.Hash(bytes.AsSpan(0, 20)));
        Assert.Equal(20, (await first.InvokeAsync<HubImageChunkReceipt>("UploadImageChunk", chunk)).NextOffset);
        await first.StopAsync();
        await WaitForDisconnectAsync(env.Ingress);
        await using var resumed = Connection(app, transport); await resumed.StartAsync();
        var resumedRegistration = await resumed.InvokeAsync<FusionRegistration>("RegisterFusion", FusionIngressTestEnvironment.Hello());
        await Assert.ThrowsAsync<HubException>(() => resumed.InvokeAsync<HubImageChunkReceipt>("UploadImageChunk", chunk));
        var resumedBegin = await resumed.InvokeAsync<HubImageBeginReceipt>("BeginImageUpload", descriptor with { LeaseId = resumedRegistration.LeaseId });
        Assert.Equal(20, resumedBegin.NextOffset);
        var tail = bytes.AsSpan(20).ToArray();
        await resumed.InvokeAsync<HubImageChunkReceipt>("UploadImageChunk", new HubImageChunk(begin.UploadId, 20, Convert.ToBase64String(tail), FusionProtocol.Hash(tail)));
        var stored = await resumed.InvokeAsync<HubImageStoredReceipt>("CompleteImageUpload", new HubImageComplete(begin.UploadId, descriptor.SourceImageId, bytes.Length, descriptor.ContentSha256));
        Assert.Equal("LocalDurable", stored.StorageProvider);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/parcels/fusion/sources")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/parcels/fusion/images/" + stored.ObjectKey + "/content")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(bytes, await client.GetByteArrayAsync("/api/parcels/fusion/images/" + stored.ObjectKey + "/content"));
        Assert.NotEmpty((await client.GetFromJsonAsync<FusionSourceStatus[]>("/api/parcels/fusion/sources"))!);
        Assert.Single((await client.GetFromJsonAsync<FusionFactInspection[]>("/api/diagnostics/fusion/facts?sourceInstanceId=fusion-line-01"))!);
        Assert.Equal(1024 * 1024, app.Services.GetRequiredService<IOptions<HubOptions<FusionIngestionHub>>>().Value.MaximumReceiveMessageSize);
        Assert.Equal(16384, app.Services.GetRequiredService<IOptions<HubOptions>>().Value.MaximumReceiveMessageSize);
        await resumed.StopAsync();
        await WaitForDisconnectAsync(env.Ingress);
        Assert.All(await env.Ingress.GetSourcesAsync(default), source => Assert.False(source.IsOnline));
    }

    /// <summary>网页 Cookie、旧机器密钥、其他来源的凭据和 URL 参数都不能进入专用机器通道。</summary>
    [Fact]
    public async Task MachineAuthenticationIsIndependentAndSourceBound() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env); using var client = app.GetTestClient();
        const string path = "/hubs/fusion-ingestion/negotiate?negotiateVersion=1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path, null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path, null)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Sorting-Api-Key", "test-machine-key");
        client.DefaultRequestHeaders.Add("X-Fusion-SourceId", "fusion-line-02");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FusionIngressTestEnvironment.FirstKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path, null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        const string queryCredentialTemplate = "&access_token=${FUSION_TEST_KEY}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(path + queryCredentialTemplate.Replace("${FUSION_TEST_KEY}", FusionIngressTestEnvironment.SecondKey, StringComparison.Ordinal), null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FusionIngressTestEnvironment.SecondKey);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, null)).StatusCode);
        await using var first = Connection(app, HttpTransportType.WebSockets); await first.StartAsync();
        await first.InvokeAsync<FusionRegistration>("RegisterFusion", FusionIngressTestEnvironment.Hello());
        await using var clone = Connection(app, HttpTransportType.WebSockets); await clone.StartAsync();
        Assert.Contains("SourceInstanceAlreadyConnected", (await Assert.ThrowsAsync<HubException>(() => clone.InvokeAsync<FusionRegistration>("RegisterFusion", FusionIngressTestEnvironment.Hello()))).Message);
        await clone.StopAsync();
        await first.StopAsync();
        await WaitForDisconnectAsync(env.Ingress);
    }

    /// <summary>真实 Fusion 诊断路由在两种权限配置下均拒绝普通全权限账号，业务来源读取仍可用。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RawFactsRequireSuperAdministratorEvenWhenGeneralAuthorizationIsDisabled(bool enforceAuthorization) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env, enforceAuthorization);
        using var admin = app.GetTestClient(); using var ordinary = app.GetTestClient();
        using var anonymous = app.GetTestClient(); using var builtIn = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); ordinary.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); builtIn.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "超级管理员", permissions = AccessDirectoryService.PermissionCodes })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "ordinary", name = "普通角色", password = "test-ordinary-password", roleId = 2 })).StatusCode);
        AccessApiTests.UseCookie(ordinary, await ordinary.PostAsJsonAsync("/api/access/login", new { username = "ordinary", password = "test-ordinary-password" }));
        AccessApiTests.UseCookie(builtIn, await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = "15876396602" }));
        const string path = "/api/diagnostics/fusion/facts?sourceInstanceId=fusion-line-01";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await builtIn.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinary.GetAsync("/api/parcels/fusion/sources")).StatusCode);
    }

    /// <summary>客户端停止可能先于服务端断线回调完成，等待耐久租约释放后才重连或回收测试数据库。</summary>
    private static async Task WaitForDisconnectAsync(FusionIngestionService ingress) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while ((await ingress.GetSourcesAsync(deadline.Token)).Any(source => source.IsOnline))
            await Task.Delay(20, deadline.Token);
    }


    /// <summary>管理员在线登记与密钥轮换通过生产路由，公共读取和无租约探测不泄露密钥。</summary>
    [Fact]
    public async Task OnlineDirectoryUsesProtectedRoutesAndProbeDoesNotTakeLease() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env);
        using var client = app.GetTestClient();
        const string path = "/api/operations/configuration/fusion";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        var view = (await client.GetFromJsonAsync<FusionConfigurationView>(path))!;
        var created = await client.PostAsJsonAsync(path + "/sources", new FusionSourceChange(view.Revision, new("fusion-ui-02", "第二工作台", true, "default", "default", "line-01", "", "", "Asia/Shanghai")));
        created.EnsureSuccessStatusCode();
        Assert.Contains("no-store", created.Headers.CacheControl!.ToString());
        var pairing = (await created.Content.ReadFromJsonAsync<FusionPairingResult>())!;
        var publicJson = await client.GetStringAsync(path);
        Assert.DoesNotContain(pairing.Pairing.MachineApiKey, publicJson);
        Assert.DoesNotContain("machineApiKey", publicJson);
        var stale = await client.PostAsJsonAsync(path + "/sources/fusion-ui-02/rotate-key", new FusionKeyRotation(view.Revision));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var machine = Connection(app, HttpTransportType.LongPolling);
        await machine.StartAsync();
        var check = await machine.InvokeAsync<FusionConfigurationCheck>("CheckFusionConfiguration", new FusionConfigurationProbe("fusion-line-01", "sorting-hub", "line-01", "Asia/Shanghai", "", ""));
        Assert.True(check.Matched);
        var directory = (await client.GetFromJsonAsync<FusionConfigurationView>(path))!;
        Assert.False(directory.Sources.Single(x => x.SourceInstanceId == "fusion-line-01").IdentityLocked);
        var registration = await machine.InvokeAsync<FusionRegistration>("RegisterFusion", FusionIngressTestEnvironment.Hello() with { SiteCode = "", DeviceCode = "" });
        Assert.NotEmpty(registration.LeaseId);
        await machine.StopAsync();
        await WaitForDisconnectAsync(env.Ingress);
    }

    /// <summary>删除接口检查账号权限、写入来源、目录版本和接入历史，响应不返回配对凭据。</summary>
    [Fact]
    public async Task DeleteSourceRequiresPermissionAndCurrentRevisionAndPreservesUsedSource() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env, enforceAuthorization: false);
        using var admin = app.GetTestClient(); using var viewer = app.GetTestClient();
        const string path = "/api/operations/configuration/fusion";
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.DeleteAsync(path + "/sources/fusion-line-02?revision=1")).StatusCode);
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); viewer.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "查询员", permissions = CachedSettingsReadParcelsReadValues })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "viewer", name = "查询员", password = "test-viewer-password", roleId = 2 })).EnsureSuccessStatusCode();
        AccessApiTests.UseCookie(viewer, await viewer.PostAsJsonAsync("/api/access/login", new { username = "viewer", password = "test-viewer-password" }));
        var directory = (await admin.GetFromJsonAsync<FusionConfigurationView>(path))!;
        var deletePath = path + "/sources/fusion-line-02?revision=" + directory.Revision;
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.DeleteAsync(deletePath)).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Zeye-Client");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.DeleteAsync(deletePath)).StatusCode);
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(path + "/sources/fusion-line-02?revision=" + (directory.Revision + 1))).StatusCode);
        await env.Ingress.RegisterAsync("used", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var denied = await admin.DeleteAsync(path + "/sources/fusion-line-01?revision=" + directory.Revision);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var deleted = await admin.DeleteAsync(deletePath);
        deleted.EnsureSuccessStatusCode();
        Assert.Contains("no-store", deleted.Headers.CacheControl!.ToString());
        var text = await deleted.Content.ReadAsStringAsync();
        Assert.DoesNotContain("machineApiKey", text); Assert.DoesNotContain(FusionIngressTestEnvironment.SecondKey, text);
        var remaining = (await deleted.Content.ReadFromJsonAsync<FusionConfigurationView>())!;
        Assert.Equal("fusion-line-01", Assert.Single(remaining.Sources).SourceInstanceId);
        Assert.True(remaining.Revision > directory.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync(deletePath)).StatusCode);
    }

    /// <summary>正式配置组在实时通道初始化前注册，流式读取复用权限并及时返回无密钥的在线修改。</summary>
    [Theory]
    [InlineData(true, HttpTransportType.WebSockets)]
    [InlineData(false, HttpTransportType.LongPolling)]
    public async Task OnlineDirectorySupportsAuthenticatedRealtimeReadAndWatch(bool enforceAuthorization, HttpTransportType transport) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var app = await CreateAsync(env, enforceAuthorization);
        using var admin = app.GetTestClient(); admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "配置查询员", permissions = CachedParcelsReadSettingsReadValues });
        await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "viewer", name = "查询员", password = "test-viewer-password", roleId = 2 });
        using var viewer = app.GetTestClient(); viewer.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(viewer, await viewer.PostAsJsonAsync("/api/access/login", new { username = "viewer", password = "test-viewer-password" }));
        const string path = "/api/operations/configuration/fusion";
        await using var denied = SortingConnection(app, viewer, transport); await denied.StartAsync();
        Assert.Equal(403, (await denied.InvokeAsync<RealtimeResponse>("Read", path)).StatusCode);
        await using var realtime = SortingConnection(app, admin, transport); await realtime.StartAsync();
        var result = await realtime.InvokeAsync<RealtimeResponse>("Read", path);
        Assert.Equal(200, result.StatusCode); Assert.Equal(await admin.GetStringAsync(path), result.Json);
        Assert.DoesNotContain("machineApiKey", result.Json); Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, result.Json);
        Assert.Equal(200, (await realtime.InvokeAsync<RealtimeResponse>("Read", "/api/parcels/fusion/sources")).StatusCode);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = realtime.StreamAsync<RealtimeResponse>("Watch", path, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(200, stream.Current.StatusCode);
        var view = JsonSerializer.Deserialize<FusionConfigurationView>(stream.Current.Json, JsonSerializerOptions.Web)!;
        var response = await admin.PostAsJsonAsync(path + "/sources", new FusionSourceChange(view.Revision,
            new("fusion-realtime-03", "第三工作台", true, "default", "default", "line-03", "", "", "Asia/Shanghai")));
        response.EnsureSuccessStatusCode(); var created = (await response.Content.ReadFromJsonAsync<FusionPairingResult>())!;
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(200, stream.Current.StatusCode);
        Assert.Contains("fusion-realtime-03", stream.Current.Json); Assert.DoesNotContain(created.Pairing.MachineApiKey, stream.Current.Json);
        await cancellation.CancelAsync();
    }

    /// <summary>浏览器实时查询使用正式 Cookie 和端点分发，机器专用通道不参与配置管理。</summary>
    private static HubConnection SortingConnection(WebApplication app, HttpClient client, HttpTransportType transport) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/sorting", options => {
            var cookie = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            options.Transports = transport; options.Headers["Cookie"] = cookie;
            options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
            options.WebSocketFactory = async (context, token) => {
                var socket = app.GetTestServer().CreateWebSocketClient(); socket.ConfigureRequest = request => request.Headers.Cookie = cookie;
                return await socket.ConnectAsync(context.Uri, token);
            };
        }).Build();

    /// <summary>安装生产入口，只有后台轮询任务被移除以便逐步检查确认前后的状态。</summary>
    private static async Task<WebApplication> CreateAsync(FusionIngressTestEnvironment env, bool enforceAuthorization = true) {
        var app = await AccessApiTests.CreateAsync(env.Database, enforceAuthorization,
        configureServices: builder => {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FusionIngestion:AllowInsecureHttp"] = "true" });
            builder.Services.AddSortingRealtime();
            builder.Services.Configure<WebRequestAuditLogOptions>(options => options.Enabled = false);
            builder.Services.AddSingleton(new WebRequestAuditBuffer(32, TimeSpan.FromSeconds(30)));
            builder.Services.AddFusionIngestion(builder.Configuration, env.Root);
            builder.Services.AddSingleton<IOptions<FusionIngestionOptions>>(Microsoft.Extensions.Options.Options.Create(env.Options));
            builder.Services.RemoveAll<IHostedService>();
            builder.Services.AddSingleton<IFusionIngestionGateway>(env.Ingress);
        }, configureRoutes: app => { app.UseSortingRealtime(); app.MapFusionIngestion(); app.MapFusionConfiguration(); app.MapSortingRealtime(); });
        await app.Services.GetRequiredService<FusionConfigurationService>().InitializeAsync(default);
        return app;
    }

    /// <summary>机器请求头同时传入 HTTP 协商与真实 WebSocket，不借用浏览器 Cookie。</summary>
    private static HubConnection Connection(WebApplication app, HttpTransportType transport) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/fusion-ingestion", options => {
            options.Transports = transport;
            options.Headers["Authorization"] = "Bearer " + FusionIngressTestEnvironment.FirstKey;
            options.Headers["X-Fusion-SourceId"] = "fusion-line-01";
            options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
            options.WebSocketFactory = async (context, token) => {
                var client = app.GetTestServer().CreateWebSocketClient();
                client.ConfigureRequest = request => { request.Headers.Authorization = "Bearer " + FusionIngressTestEnvironment.FirstKey; request.Headers["X-Fusion-SourceId"] = "fusion-line-01"; };
                return await client.ConnectAsync(context.Uri, token);
            };
        }).Build();
}
