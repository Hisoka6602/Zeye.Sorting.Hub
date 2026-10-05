using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证协议签名兼容、过期、来源边界及可达地址。</summary>
public sealed class FusionDiscoveryTests {
    /// <summary>已有进程占用可选发现端口时，仅进入后台重试，取消可立即结束等待。</summary>
    [Fact]
    public async Task OptionalDiscoveryPortConflictDoesNotFaultHostWorker() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        using var holder = new UdpClient(0);
        env.Options.DiscoveryEnabled = true; env.Options.DiscoveryPort = ((IPEndPoint)holder.Client.LocalEndPoint!).Port;
        env.Options.AdvertisedEndpoint = "https://hub.example.test/hubs/fusion-ingestion";
        var discovery = new FusionDiscoveryService(Microsoft.Extensions.Options.Options.Create(env.Options));
        using var worker = new HostedServices.FusionDiscoveryHostedService(discovery);
        await worker.StartAsync(default);
        await Task.Delay(100);
        Assert.False(worker.ExecuteTask!.IsCompleted);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(deadline.Token);
        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
    }
    /// <summary>真实 UDP 收发验证取消释放监听端口，发现响应携带合法签名及原 nonce。</summary>
    [Fact]
    public async Task DiscoveryUsesRealSocketAndReleasesPortOnStop() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        int port;
        using (var probe = new UdpClient(0)) port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        env.Options.DiscoveryEnabled = true; env.Options.DiscoveryPort = port;
        env.Options.AdvertisedEndpoint = "https://hub.example.test/hubs/fusion-ingestion";
        var discovery = new FusionDiscoveryService(Microsoft.Extensions.Options.Options.Create(env.Options));
        using var stop = new CancellationTokenSource();
        var listener = discovery.ListenAsync(stop.Token);
        using var client = new UdpClient();
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var packet = new FusionDiscoveryPacket("discover", "1.0", "fusion-line-01", "sorting-hub", new string('b', 32), now, now + 10000, "", "");
        packet = packet with { Signature = FusionDiscoveryService.Sign(packet, FusionIngressTestEnvironment.FirstKey) };
        await client.SendAsync(JsonSerializer.SerializeToUtf8Bytes(packet, FusionProtocol.Json), new IPEndPoint(IPAddress.Loopback, port));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reply = await client.ReceiveAsync(deadline.Token);
        var offer = JsonSerializer.Deserialize<FusionDiscoveryPacket>(reply.Buffer, FusionProtocol.Json)!;
        Assert.Equal(packet.Nonce, offer.Nonce); Assert.Equal("offer", offer.Type);
        Assert.Equal(FusionDiscoveryService.Sign(offer, FusionIngressTestEnvironment.FirstKey), offer.Signature);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener);
        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Any, port));
    }
    /// <summary>合法来源收到相同 nonce 的认证地址，显式错误协议或内容变更不能被接受。</summary>
    [Fact]
    public async Task AuthenticatedDiscoveryRejectsTamperingExpiryAndUnknownSources() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        env.Options.AdvertisedEndpoint = "https://hub.example.test/hubs/fusion-ingestion";
        var service = new FusionDiscoveryService(Microsoft.Extensions.Options.Options.Create(env.Options));
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var query = new FusionDiscoveryPacket("discover", "1.0", "fusion-line-01", "sorting-hub", new string('a', 32), now, now + 10000, "", "");
        query = query with { Signature = FusionDiscoveryService.Sign(query, FusionIngressTestEnvironment.FirstKey) };
        var offer = service.Offer(JsonSerializer.SerializeToUtf8Bytes(query, FusionProtocol.Json), IPAddress.Parse("192.0.2.1"));
        Assert.NotNull(offer); Assert.Equal(query.Nonce, offer.Nonce); Assert.Equal(query.ExpiresAtUnixMs, offer.ExpiresAtUnixMs);
        Assert.Equal(FusionDiscoveryService.Sign(offer, FusionIngressTestEnvironment.FirstKey), offer.Signature);
        foreach (var bad in new[] { query with { Protocol = "other.application" }, query with { SourceInstanceId = "unknown" },
            query with { Nonce = new string('b', 32) }, query with { ExpiresAtUnixMs = now - 1 }, query with { SentAtUnixMs = now + 3000 } })
            Assert.Null(service.Offer(JsonSerializer.SerializeToUtf8Bytes(bad, FusionProtocol.Json), IPAddress.Loopback));
        var legacy = JsonSerializer.Serialize(query, FusionProtocol.Json).Replace(",\"protocol\":\"zeye.fusion-hub.discovery\"", "", StringComparison.Ordinal);
        Assert.NotNull(service.Offer(System.Text.Encoding.UTF8.GetBytes(legacy), IPAddress.Loopback));
        Assert.Null(service.Offer(new byte[4097], IPAddress.Loopback));
        env.Options.AdvertisedEndpoint = "http://127.0.0.1/hubs/fusion-ingestion";
        Assert.Null(service.Offer(JsonSerializer.SerializeToUtf8Bytes(query, FusionProtocol.Json), IPAddress.Parse("192.0.2.1")));
    }
    /// <summary>仅允许专用 Hub 路径及显式许可的测试 HTTP 地址。</summary>
    [Theory]
    [InlineData("https://hub.example.test/hubs/fusion-ingestion", false, true)]
    [InlineData("http://hub.example.test/hubs/fusion-ingestion", false, false)]
    [InlineData("http://hub.example.test/hubs/fusion-ingestion", true, true)]
    [InlineData("https://user:secret@hub.example.test/hubs/fusion-ingestion", false, false)]
    [InlineData("https://hub.example.test/hubs/fusion-ingestion?access_token=${TEST_MACHINE_KEY}", false, false)]
    [InlineData("https://hub.example.test/hubs/sorting", false, false)]
    [InlineData("http://0.0.0.0/hubs/fusion-ingestion", true, false)]
    public void DiscoveryEndpointMustBeSafeAndDedicated(string endpoint, bool insecure, bool expected) =>
        Assert.Equal(expected, FusionDiscoveryService.IsEndpoint(endpoint, insecure));
}
