using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Realtime;
using Zeye.Sorting.Hub.Host.Extensions;
using Zeye.Sorting.Hub.Host.Hubs;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>使用生产认证与正式实时入口验证握手、读取、变更推送、取消和权限撤销。</summary>
public sealed class RealtimeApiTests {
    /// <summary>三个敏感版块的列表、明细及深度诊断实时读取入口。</summary>
    private static readonly string[] RestrictedReadPaths = ["/api/audit/web-requests", "/api/audit/web-requests/123",
        "/api/diagnostics/slow-queries", "/api/diagnostics/slow-queries/test", "/api/data-governance/archive-tasks",
        "/api/operations/partitions", "/health/deep"];

    /// <summary>拥有全部单项权限的普通角色也不能通过实时读取、订阅和命名提交绕过敏感版块边界。</summary>
    [Theory]
    [InlineData(true, HttpTransportType.WebSockets)]
    [InlineData(false, HttpTransportType.WebSockets)]
    [InlineData(true, HttpTransportType.LongPolling)]
    [InlineData(false, HttpTransportType.LongPolling)]
    public async Task SensitiveRealtimeResourcesRequireSuperAdministratorIdentity(bool enforceAuthorization, HttpTransportType transport) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization); using var admin = app.GetTestClient(); await LoginAsync(admin);
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "超级管理员", permissions = AccessDirectoryService.PermissionCodes });
        await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "ordinary", name = "普通用户", roleId = 2, password = "test-ordinary-password" });
        using var ordinary = app.GetTestClient(); ordinary.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(ordinary, await ordinary.PostAsJsonAsync("/api/access/login", new { username = "ordinary", password = "test-ordinary-password" }));
        await using var hub = CreateConnection(app, ordinary, transport); await hub.StartAsync();
        foreach (var path in RestrictedReadPaths) {
            Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("Read", path)).StatusCode);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await using var stream = hub.StreamAsync<RealtimeResponse>("Watch", path, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
            Assert.True(await stream.MoveNextAsync()); Assert.Equal(403, stream.Current.StatusCode); Assert.False(await stream.MoveNextAsync());
        }
        Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", "{}")).StatusCode);
        Assert.Equal(200, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        Assert.Equal(200, (await hub.InvokeAsync<RealtimeResponse>("UpdateParcelStatus", "1", "{\"status\":2}")).StatusCode);
        using var builtIn = app.GetTestClient(); builtIn.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(builtIn, await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = "15876396602" }));
        foreach (var client in new[] { admin, builtIn }) {
            await using var privilegedHub = CreateConnection(app, client, transport); await privilegedHub.StartAsync();
            foreach (var path in RestrictedReadPaths)
                Assert.Equal(path == "/health/deep" ? 503 : 200, (await privilegedHub.InvokeAsync<RealtimeResponse>("Read", path)).StatusCode);
            Assert.Equal(200, (await privilegedHub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", "{}")).StatusCode);
        }
    }

    /// <summary>已建立的超级管理员实时连接在降级后立即停止敏感订阅，重新登录也只能按普通身份访问。</summary>
    [Theory]
    [InlineData(true, HttpTransportType.WebSockets)]
    [InlineData(false, HttpTransportType.LongPolling)]
    public async Task ExistingSuperAdministratorConnectionLosesRestrictedAccessAfterDemotion(bool enforceAuthorization, HttpTransportType transport) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization); using var admin = app.GetTestClient(); await LoginAsync(admin);
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "全部业务权限", permissions = AccessDirectoryService.PermissionCodes });
        await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "delegate", name = "临时管理员", roleId = 1, password = "test-delegate-password" });
        using var member = app.GetTestClient(); member.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(member, await member.PostAsJsonAsync("/api/access/login", new { username = "delegate", password = "test-delegate-password" }));
        await using var hub = CreateConnection(app, member, transport); await hub.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var stream = hub.StreamAsync<RealtimeResponse>("Watch", "/api/audit/web-requests", cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await stream.MoveNextAsync()); Assert.Equal(200, stream.Current.StatusCode);
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        var memberId = directory.GetProperty("users").EnumerateArray().Single(user => user.GetProperty("account").GetString() == "delegate").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { id = memberId, expectedRevision = directory.GetProperty("revision").GetInt32(), account = "delegate", name = "普通成员", roleId = 2 })).StatusCode);
        var revocation = await Record.ExceptionAsync(async () => {
            Assert.True(await stream.MoveNextAsync()); Assert.Equal(401, stream.Current.StatusCode); Assert.False(await stream.MoveNextAsync());
        });
        // 长轮询可能先在 HTTP 重新鉴权时关闭传输，两种时序都只接受明确的 401 拒绝。
        if (transport == HttpTransportType.LongPolling && revocation is HttpRequestException rejected)
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        else Assert.Null(revocation);
        if (transport == HttpTransportType.WebSockets)
            Assert.Equal(401, (await hub.InvokeAsync<RealtimeResponse>("Read", "/health/deep")).StatusCode);
        await hub.StopAsync();
        AccessApiTests.UseCookie(member, await member.PostAsJsonAsync("/api/access/login", new { username = "delegate", password = "test-delegate-password" }));
        await using var newHub = CreateConnection(app, member, transport); await newHub.StartAsync();
        Assert.Equal(403, (await newHub.InvokeAsync<RealtimeResponse>("Read", "/api/audit/web-requests")).StatusCode);
    }

    /// <summary>频繁长轮询消息不消耗握手预算，新的连接仍保持每分钟六十次的限流。</summary>
    [Fact]
    public async Task LongPollingMessagesDoNotConsumeNegotiationBudget() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient(); await LoginAsync(client);
        await using var first = CreateConnection(app, client, HttpTransportType.LongPolling); await first.StartAsync();
        for (var index = 0; index < 90; index++)
            Assert.Equal(200, (await first.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        await using var second = CreateConnection(app, client, HttpTransportType.LongPolling); await second.StartAsync();
        Assert.Equal(200, (await second.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        // 前两个客户端各协商一次，其余五十八次协商用尽额度后，下一次必须拒绝。
        for (var index = 0; index < 58; index++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/sorting/negotiate?negotiateVersion=1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/hubs/sorting/negotiate?negotiateVersion=1", null)).StatusCode);
    }
    /// <summary>危险、外部、非规范路径不能经只读通道执行。</summary>
    [Theory]
    [InlineData("/api/admin/parcels/cleanup-expired")]
    [InlineData("/api/access")]
    [InlineData("/api/access/profile/avatar")]
    [InlineData("/api/operations/backup/artifacts/test/download")]
    [InlineData("https://example.test/api/parcels")]
    [InlineData("//example.test/api/parcels")]
    [InlineData("/api/parcels/../access")]
    [InlineData("/api/parcels/%2e%2e/access")]
    [InlineData("/api/parcels#fragment")]
    [InlineData("/api/parcels/0")]
    [InlineData("/api/parcels/1/unknown")]
    [InlineData("/hubs/sorting")]
    public void UnsafeResourcesAreRejected(string path) => Assert.False(RealtimeReadPolicy.IsAllowed(path));

    /// <summary>匿名不能握手，跨站来源不能借助已有 Cookie 建立实时通道。</summary>
    [Fact]
    public async Task NegotiationRequiresCookieAndSameOrigin() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db);
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/hubs/sorting/negotiate?negotiateVersion=1", null)).StatusCode);
        await LoginAsync(client);
        client.DefaultRequestHeaders.Add("Origin", "https://example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/hubs/sorting/negotiate?negotiateVersion=1", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "http://localhost");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/sorting/negotiate?negotiateVersion=1", null)).StatusCode);
    }

    /// <summary>真实 SignalR 客户端保持编号原文、状态码和查询参数，拒绝危险读取入口。</summary>
    [Theory]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.LongPolling)]
    public async Task ReadUsesExistingRoutesAndPreservesJsonAndStatuses(HttpTransportType transport) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient(); await LoginAsync(client);
        await using var hub = CreateConnection(app, client, transport); await hub.StartAsync();
        var result = await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels?barCodeKeyword=SF%2B001");
        Assert.Equal(200, result.StatusCode); Assert.Contains("9223372036854775806", result.Json); Assert.Contains("SF+001", result.Json);
        Assert.Equal(await client.GetStringAsync("/api/parcels?barCodeKeyword=SF%2B001"), result.Json);
        Assert.Equal(400, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/admin/parcels/cleanup-expired")).StatusCode);
        Assert.Equal(503, (await hub.InvokeAsync<RealtimeResponse>("Read", "/health/deep")).StatusCode);
        Assert.Equal(404, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels/999")).StatusCode);
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<RealtimeResponse>("Request", "/api/admin/parcels"));
    }

    /// <summary>成功 HTTP 写入唤醒真实流，取消后立即释放有界订阅。</summary>
    [Fact]
    public async Task StreamUpdatesAfterWriteAndReleasesSubscriptionOnCancel() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient(); await LoginAsync(client);
        await using var hub = CreateConnection(app, client); await hub.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var stream = hub.StreamAsync<RealtimeResponse>("Watch", "/api/operations/rules/parcel", cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await stream.MoveNextAsync()); Assert.Contains("\"revision\":0", stream.Current.Json);
        var write = await client.PutAsJsonAsync("/api/operations/rules/parcel", new { expectedRevision = 0, rules = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
        Assert.True(await stream.MoveNextAsync()); Assert.Contains("\"revision\":1", stream.Current.Json);
        await cancellation.CancelAsync();
    }

    /// <summary>命名提交复用 JSON 绑定、原权限和变更通知，不接受自由路径或超预算正文。</summary>
    [Fact]
    public async Task NamedCommandsKeepOriginalBindingAndBoundaries() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient(); await LoginAsync(client);
        await using var hub = CreateConnection(app, client); await hub.StartAsync();
        var wakeup = app.Services.GetRequiredService<RealtimeResourceSignal>().Change;
        var result = await hub.InvokeAsync<RealtimeResponse>("UpdateParcelStatus", "9223372036854775806", "{\"status\":2}");
        Assert.Equal(200, result.StatusCode); Assert.Contains("9223372036854775806", result.Json); Assert.Contains("\"status\":2", result.Json);
        Assert.True(wakeup.IsCompletedSuccessfully);
        Assert.Equal(200, (await hub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", "{\"recordId\":\"test\"}")).StatusCode);
        Assert.Equal(400, (await hub.InvokeAsync<RealtimeResponse>("UpdateParcelStatus", "../cleanup-expired", "{}")).StatusCode);
        Assert.Equal(400, (await hub.InvokeAsync<RealtimeResponse>("UpdateParcelStatus", "1", "invalid-json")).StatusCode);
        Assert.Equal(413, (await hub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", new string('中', 1366))).StatusCode);
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<RealtimeResponse>("Delete", "1"));
    }

    /// <summary>同一旧长连接不能保留已撤销的角色权限，也不能继续读取停用账号的数据。</summary>
    [Fact]
    public async Task ExistingConnectionRevalidatesRolesAndAccountDisable() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var admin = app.GetTestClient(); await LoginAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "查询员", permissions = new[] { "parcels.read" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "reader", name = "查询员", password = "test-reader-password", roleId = 2, enabled = true })).StatusCode);
        using var reader = app.GetTestClient(); reader.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(reader, await reader.PostAsJsonAsync("/api/access/login", new { username = "reader", password = "test-reader-password" }));
        await using var hub = CreateConnection(app, reader); await hub.StartAsync();
        Assert.Equal(200, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/operations/rules/parcel")).StatusCode);
        Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", "{}")).StatusCode);
        Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("UpdateParcelStatus", "1", "{\"status\":2}")).StatusCode);
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { id = 2, expectedRevision = directory.GetProperty("revision").GetInt32(), name = "查询员", permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(403, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        var id = directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "reader").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { id, expectedRevision = directory.GetProperty("revision").GetInt32(), account = "reader", name = "查询员", roleId = 2, enabled = false })).StatusCode);
        Assert.Equal(401, (await hub.InvokeAsync<RealtimeResponse>("Read", "/api/parcels")).StatusCode);
        Assert.Equal(401, (await hub.InvokeAsync<RealtimeResponse>("AppendProcessingRecord", "{}")).StatusCode);
    }

    /// <summary>每连接订阅有上限，释放后能够复用槽位，变更唤醒不会保留旧任务。</summary>
    [Fact]
    public void SubscriptionCapacityAndWakeupAreBounded() {
        var signal = new RealtimeResourceSignal(); var wakeup = signal.Change;
        for (var index = 0; index < 16; index++) Assert.True(signal.TrySubscribe("test"));
        Assert.False(signal.TrySubscribe("test")); signal.Unsubscribe("test"); Assert.True(signal.TrySubscribe("test"));
        signal.Notify(); Assert.True(wakeup.IsCompletedSuccessfully); Assert.False(signal.Change.IsCompleted);
        for (var index = 0; index < 16; index++) signal.Unsubscribe("test");
        Assert.True(signal.TrySubscribe("test")); signal.Unsubscribe("test");
    }

    /// <summary>为隔离账号数据库注册正式实时配置与确定性的测试读取端点。</summary>
    private static Task<WebApplication> CreateAsync(RelationalParcelTestDatabase db, bool enforceAuthorization = true) => AccessApiTests.CreateAsync(db, enforceAuthorization,
        configureServices: builder => {
            builder.Services.AddSortingRealtime();
            builder.Services.Configure<WebRequestAuditLogOptions>(options => options.Enabled = false);
            builder.Services.AddSingleton(new WebRequestAuditBackgroundQueue(32, TimeSpan.FromSeconds(30)));
        }, configureRoutes: app => {
            app.UseSortingRealtime();
            app.MapGet("/api/parcels", (HttpContext context) => Results.Ok(new { id = 9223372036854775806L, query = context.Request.Query["barCodeKeyword"].ToString() }));
            app.MapGet("/health/deep", () => Results.Json(new { status = "Unhealthy", entries = new { backup = new { status = "Degraded" } } }, statusCode: 503));
            foreach (var path in RestrictedReadPaths.Where(path => path != "/health/deep")) app.MapGet(path, () => Results.Ok());
            app.MapPut("/api/admin/parcels/{id:long}", (long id, JsonElement body) => Results.Ok(new { id, status = body.GetProperty("status").GetInt32() }));
            app.MapSortingRealtime();
        });

    /// <summary>登录测试专用管理员，Cookie 与实际浏览器会话语义一致。</summary>
    private static async Task LoginAsync(HttpClient client) {
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
    }

    /// <summary>官方客户端走 TestServer 的真实 WebSocket 或长轮询，不用模拟 Hub 方法调用。</summary>
    private static HubConnection CreateConnection(WebApplication app, HttpClient client, HttpTransportType transport = HttpTransportType.WebSockets) => new HubConnectionBuilder().WithUrl("http://localhost/hubs/sorting", options => {
        options.Transports = transport;
        options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
        var cookie = client.DefaultRequestHeaders.GetValues("Cookie").Single();
        options.Headers["Cookie"] = cookie;
        options.WebSocketFactory = async (context, ct) => {
            var socketClient = app.GetTestServer().CreateWebSocketClient();
            socketClient.ConfigureRequest = request => request.Headers.Cookie = cookie;
            return await socketClient.ConnectAsync(context.Uri, ct);
        };
    }).Build();
}
