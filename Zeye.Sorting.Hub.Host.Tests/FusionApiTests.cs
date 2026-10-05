using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
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
using Zeye.Sorting.Hub.Host.Extensions;
using Zeye.Sorting.Hub.Host.Hubs;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>通过官方 SignalR 客户端验证生产认证、完整协议方法和受保护的读取路径。</summary>
public sealed class FusionApiTests {
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
        for (var attempt = 0; attempt < 100 && (await env.Ingress.GetSourcesAsync(default)).Any(x => x.IsOnline); attempt++)
            await Task.Delay(20);
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
    }

    /// <summary>安装生产入口，只有后台轮询任务被移除以便逐步检查确认前后的状态。</summary>
    private static Task<WebApplication> CreateAsync(FusionIngressTestEnvironment env) => AccessApiTests.CreateAsync(env.Database,
        configureServices: builder => {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FusionIngestion:AllowInsecureHttp"] = "true" });
            builder.Services.AddSortingRealtime();
            builder.Services.AddFusionIngestion(builder.Configuration, env.Root);
            builder.Services.RemoveAll<IHostedService>();
            builder.Services.AddSingleton<IFusionIngestionGateway>(env.Ingress);
        }, configureRoutes: app => app.MapFusionIngestion());

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
