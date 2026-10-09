using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MsOptions = Microsoft.Extensions.Options.Options;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证在线目录、加密凭据和租约隔离。</summary>
public sealed class FusionConfigurationTests {
    /// <summary>工作台登记及编辑请求。</summary>
    private static FusionSourceWrite Source(string id = "fusion-new") => new(id, "分拣工作台", true, "default", "default", "line-01", "", "", "Asia/Shanghai");

    /// <summary>维护接入目录的 NewSourceIsImmediatelyAuthenticated_EncryptedAtRest_AndSurvivesRestart 配置与生命周期。</summary>
    [Fact]
    public async Task NewSourceIsImmediatelyAuthenticated_EncryptedAtRest_AndSurvivesRestart() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var protection = new EphemeralDataProtectionProvider();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection);
        await service.InitializeAsync(default);
        var view = await service.ReadAsync(default);
        var created = await service.CreateSourceAsync(view.Revision, Source(), default);
        Assert.NotNull(created);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, service);
        Assert.True(ingress.Authenticate("fusion-new", created.Pairing.MachineApiKey));
        Assert.False(ingress.Authenticate("fusion-line-01", created.Pairing.MachineApiKey));
        await using var db = await env.Database.Factory.CreateDbContextAsync(default);
        var json = (await db.Set<ManagedDocument>().SingleAsync(x => x.Key == "fusion-ingestion-directory")).Json;
        Assert.DoesNotContain(created.Pairing.MachineApiKey, json);
        Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, json);
        Assert.DoesNotContain("machineApiKey", JsonSerializer.Serialize(await service.ReadAsync(default)));
        var restarted = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection);
        await restarted.InitializeAsync(default);
        var rebuilt = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, restarted);
        Assert.True(rebuilt.Authenticate("fusion-new", created.Pairing.MachineApiKey));
    }

    /// <summary>维护接入目录的 RotationRevokesExistingLease_StaleWritesAreRejected_AndOtherSourcesKeepWorking 配置与生命周期。</summary>
    [Fact]
    public async Task RotationRevokesExistingLease_StaleWritesAreRejected_AndOtherSourcesKeepWorking() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider());
        await service.InitializeAsync(default);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, service);
        await ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var view = await service.ReadAsync(default);
        Assert.True(view.Sources.Single(x => x.SourceInstanceId == "fusion-line-01").IdentityLocked);
        var rotated = await service.RotateKeyAsync(view.Revision, "fusion-line-01", default);
        Assert.NotNull(rotated);
        Assert.False(ingress.Authenticate("fusion-line-01", FusionIngressTestEnvironment.FirstKey));
        Assert.True(ingress.Authenticate("fusion-line-01", rotated.Pairing.MachineApiKey));
        Assert.True(ingress.Authenticate("fusion-line-02", FusionIngressTestEnvironment.SecondKey));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default));
        Assert.Null(await service.RotateKeyAsync(view.Revision, "fusion-line-01", default));
        var source = view.Sources.Single(x => x.SourceInstanceId == "fusion-line-01");
        var edited = new FusionSourceWrite(source.SourceInstanceId, "改名", false, source.TenantId, source.StoragePartitionId,
            source.LineId, source.SiteCode, source.DeviceCode, source.TimeZoneId);
        Assert.NotNull(await service.UpdateSourceAsync(rotated.Revision, source.SourceInstanceId, edited, default));
        Assert.False(ingress.Authenticate(source.SourceInstanceId, rotated.Pairing.MachineApiKey));
    }

    /// <summary>维护接入目录的 UsedIdentityCannotBeRetargeted_EmptyOptionalCodesMatch_AndProbeDoesNotAcquireLease 配置与生命周期。</summary>
    [Fact]
    public async Task UsedIdentityCannotBeRetargeted_EmptyOptionalCodesMatch_AndProbeDoesNotAcquireLease() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider());
        await service.InitializeAsync(default);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, service);
        var check = service.Check("fusion-line-01", new("fusion-line-01", "sorting-hub", "line-01", "Asia/Shanghai", "", ""));
        Assert.True(check.Matched);
        Assert.False((await service.ReadAsync(default)).Sources.First().IdentityLocked);
        await ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello() with { SiteCode = "", DeviceCode = "" }, default);
        var view = await service.ReadAsync(default);
        var altered = Source("fusion-line-01") with { LineId = "line-99" };
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateSourceAsync(view.Revision, altered.SourceInstanceId, altered, default));
        Assert.Contains("LineId", service.Check("fusion-line-01", new("fusion-line-01", "sorting-hub", "line-99", "Asia/Shanghai", null, null)).Mismatches);
        await Assert.ThrowsAsync<ArgumentException>(() => service.WriteSettingsAsync(view.Revision, view.Settings with { DiscoveryEnabled = true, DiscoveryPort = 5089 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateSourceAsync(view.Revision, Source("FUSION-LINE-01"), default));
    }

    /// <summary>删除误建记录同时撤销密钥，保护在线来源，版本冲突和重启均不会恢复旧登记。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteUnusedSourceIsDurableAndPreservesConnectedSource(bool useLiteDb) {
        using var storage = useLiteDb ? new ConfigurationTestStorage() : null;
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var protection = new EphemeralDataProtectionProvider();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection, storage?.Store);
        await service.InitializeAsync(default);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, service);
        await ingress.RegisterAsync("online", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var revision = service.Snapshot.Revision;
        Assert.Null(await service.DeleteSourceAsync(revision + 1, "fusion-line-02", default));
        Assert.True(ingress.Authenticate("fusion-line-02", FusionIngressTestEnvironment.SecondKey));
        var deleted = await service.DeleteSourceAsync(revision, "fusion-line-02", default);
        Assert.NotNull(deleted);
        Assert.False(ingress.Authenticate("fusion-line-02", FusionIngressTestEnvironment.SecondKey));
        await Assert.ThrowsAsync<ArgumentException>(() => ingress.RegisterAsync("removed", "fusion-line-02", FusionIngressTestEnvironment.Hello("fusion-line-02"), default));
        var remaining = Assert.Single(await ingress.GetSourcesAsync(default));
        Assert.Equal("fusion-line-01", remaining.SourceInstanceId); Assert.True(remaining.IsOnline);
        Assert.Null(await service.DeleteSourceAsync(revision, "fusion-line-01", default));
        var restarted = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection, storage?.Store);
        await restarted.InitializeAsync(default);
        Assert.Equal("fusion-line-01", Assert.Single((await restarted.ReadAsync(default)).Sources).SourceInstanceId);
        Assert.Equal(deleted.Revision, restarted.Snapshot.Revision);
        await Assert.ThrowsAsync<ArgumentException>(() => restarted.DeleteSourceAsync(deleted.Revision, "unknown", default));
        Assert.Equal(deleted.Revision, restarted.Snapshot.Revision);
    }

    /// <summary>离线租约及孤立的历史记录也阻止删除，不以当前在线状态代替历史保护。</summary>
    [Theory]
    [InlineData("lease")]
    [InlineData("heartbeat")]
    [InlineData("fact")]
    [InlineData("image")]
    public async Task DeleteSourceRejectsAnyExistingHistory(string history) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider());
        await service.InitializeAsync(default);
        const string source = "fusion-line-02";
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        switch (history) {
            case "lease":
                await env.Ingress.RegisterAsync("old", source, FusionIngressTestEnvironment.Hello(source), default);
                await env.Ingress.DisconnectAsync("old", default);
                break;
            case "heartbeat": db.Add(new FusionJournalHeartbeat { Key = "history", SourceInstanceId = source }); break;
            case "fact": db.Add(new FusionFactReceipt { Key = "history", SourceInstanceId = source }); break;
            case "image": db.Add(new FusionImageUpload { Key = "history", SourceInstanceId = source }); break;
        }
        await db.SaveChangesAsync();
        var revision = service.Snapshot.Revision;
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteSourceAsync(revision, source, default));
        Assert.Contains("不能删除", error.Message);
        Assert.Equal(revision, service.Snapshot.Revision);
        Assert.Contains(source, service.Snapshot.Sources.Keys);
        Assert.Equal(history == "lease" ? 1 : 0, await db.Set<FusionSourceLease>().CountAsync());
        Assert.Equal(history == "heartbeat" ? 1 : 0, await db.Set<FusionJournalHeartbeat>().CountAsync());
        Assert.Equal(history == "fact" ? 1 : 0, await db.Set<FusionFactReceipt>().CountAsync());
        Assert.Equal(history == "image" ? 1 : 0, await db.Set<FusionImageUpload>().CountAsync());
    }

    /// <summary>首次注册与删除竞争时只能有一方成功，不出现已登记租约的来源被删除。</summary>
    [Fact]
    public async Task DeleteAndFirstRegistrationCannotBothSucceed() {
        using var storage = new ConfigurationTestStorage();
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider(), storage.Store);
        await service.InitializeAsync(default);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, service);
        var revision = service.Snapshot.Revision;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = Task.Run(async () => { await start.Task; return await Record.ExceptionAsync(() => ingress.RegisterAsync("racing", "fusion-line-02", FusionIngressTestEnvironment.Hello("fusion-line-02"), default)); });
        var deletion = Task.Run(async () => { await start.Task; return await Record.ExceptionAsync(() => service.DeleteSourceAsync(revision, "fusion-line-02", default)); });
        start.SetResult();
        var errors = await Task.WhenAll(registration, deletion).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(errors, error => error is null);
        Assert.IsType<ArgumentException>(Assert.Single(errors, error => error is not null));
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.Equal(errors[0] is null, await db.Set<FusionSourceLease>().AnyAsync(row => row.SourceInstanceId == "fusion-line-02"));
        Assert.Equal(errors[0] is null, service.Snapshot.Sources.ContainsKey("fusion-line-02"));
    }

    /// <summary>发现服务启动时关闭也能在线开启、签名应答及再次关闭释放端口。</summary>
    [Fact]
    public async Task DiscoveryCanEnableAndDisableWithoutRestart() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider());
        await service.InitializeAsync(default);
        var discovery = new FusionDiscoveryService(MsOptions.Create(env.Options), service);
        using var stop = new CancellationTokenSource(); var listener = discovery.ListenAsync(stop.Token);
        Assert.False(listener.IsCompleted);
        int port; using (var probe = new UdpClient(0)) port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        var view = await service.ReadAsync(default);
        await service.WriteSettingsAsync(view.Revision, view.Settings with { DiscoveryEnabled = true, DiscoveryPort = port, AdvertisedEndpoint = "https://hub.example/hubs/fusion-ingestion" }, default);
        await Task.Delay(1200);
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var query = new Zeye.Sorting.Hub.Contracts.Models.Fusion.FusionDiscoveryPacket("discover", "1.0", "fusion-line-01", "sorting-hub", new string('d', 32), now, now + 10000, "", "");
        query = query with { Signature = FusionDiscoveryService.Sign(query, FusionIngressTestEnvironment.FirstKey) };
        using var client = new UdpClient();
        await client.SendAsync(JsonSerializer.SerializeToUtf8Bytes(query, FusionProtocol.Json), new IPEndPoint(IPAddress.Loopback, port));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var packet = await client.ReceiveAsync(deadline.Token);
        var offer = JsonSerializer.Deserialize<Zeye.Sorting.Hub.Contracts.Models.Fusion.FusionDiscoveryPacket>(packet.Buffer, FusionProtocol.Json)!;
        Assert.Equal(query.Nonce, offer.Nonce); Assert.Equal("offer", offer.Type);
        Assert.Equal(FusionDiscoveryService.Sign(offer, FusionIngressTestEnvironment.FirstKey), offer.Signature);
        view = await service.ReadAsync(default);
        await service.WriteSettingsAsync(view.Revision, view.Settings with { DiscoveryEnabled = false }, default);
        await Task.Delay(1200);
        using var rebound = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        stop.Cancel();
        var failure = await Record.ExceptionAsync(() => listener);
        Assert.True(failure is null or OperationCanceledException);
    }
}
