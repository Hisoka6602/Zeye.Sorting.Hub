using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>使用实际关系持久化验证认证、租约、逐条确认、乱序与恢复。</summary>
public sealed class FusionIngestionTests {
    /// <summary>无法提交接收簿时返回可重试，故障消除后同一原始事实仅存储一次。</summary>
    [Fact]
    public async Task ReceiptFailureNeverAcknowledgesVolatileState() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var batch = FusionIngressTestEnvironment.Batch(lease, FusionIngressTestEnvironment.Fact("parcel.detected", 1));
        env.Database.Failure.FailNextFusionReceipt = true;
        Assert.Equal("retryable", Assert.Single((await env.Ingress.PublishAsync("a", batch, default)).Records).Status);
        Assert.Empty(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default));
        Assert.Empty(await env.Ingress.ClaimProjectionsAsync(default));
        Assert.Equal("stored", Assert.Single((await env.Ingress.PublishAsync("a", batch, default)).Records).Status);
        Assert.Equal("duplicate", Assert.Single((await env.Ingress.PublishAsync("a", batch, default)).Records).Status);
        Assert.Single(await env.NewIngress().GetFactsAsync("fusion-line-01", null, 20, default));
    }
    /// <summary>不同来源拥有独立凭据，错误来源不能借用另一来源的密钥。</summary>
    [Fact]
    public async Task CredentialsAndRegistrationAreBoundToSourceMetadata() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        Assert.True(env.Ingress.Authenticate("fusion-line-01", FusionIngressTestEnvironment.FirstKey));
        Assert.False(env.Ingress.Authenticate("fusion-line-02", FusionIngressTestEnvironment.FirstKey));
        Assert.False(env.Ingress.Authenticate("FUSION-LINE-01", FusionIngressTestEnvironment.FirstKey));
        await Assert.ThrowsAsync<ArgumentException>(() => env.Ingress.RegisterAsync("a", "fusion-line-01",
            FusionIngressTestEnvironment.Hello() with { LineId = "other-line" }, default));
        var first = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        Assert.Equal(50, first.MaxBatchRecords); Assert.Equal(524288, first.MaxBatchBytes); Assert.Equal(32768, first.MaxImageChunkBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.NewIngress().RegisterAsync("b", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default));
        await env.Ingress.DisconnectAsync("a", default);
        Assert.NotEqual(first.LeaseId, (await env.NewIngress().RegisterAsync("b", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default)).LeaseId);
    }
    /// <summary>同事实补发为重复，编号或序号对应的新内容为冲突，序号空洞不会隐式确认。</summary>
    [Fact]
    public async Task ReceiptPreservesRawBytesAndIndependentUniqueIdentities() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var first = FusionIngressTestEnvironment.Fact("parcel.detected", 9007199254740993);
        var late = FusionIngressTestEnvironment.Fact("source.counter-run", 3, null, new { reason = "explicit reset", unknownField = "保留原文" });
        var result = await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first, late), default);
        Assert.All(result.Records, x => Assert.Equal("stored", x.Status));
        Assert.Equal(first.BodySha256, result.Records[0].BodySha256);
        Assert.Equal("duplicate", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first), default)).Records).Status);
        var changed = first with { BodyJson = first.BodyJson.Replace("SAME-BARCODE", "CHANGED-BARCODE", StringComparison.Ordinal) };
        changed = changed with { BodySha256 = FusionProtocol.Hash(changed.BodyJson) };
        Assert.Equal("conflict", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, changed), default)).Records).Status);
        var malformedChange = first with { BodyJson = "{}", BodySha256 = FusionProtocol.Hash("{}") };
        Assert.Equal("conflict", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, malformedChange), default)).Records).Status);
        var different = FusionIngressTestEnvironment.Fact("parcel.detected", 9007199254740993, "4");
        Assert.Equal("conflict", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, different), default)).Records).Status);
        var facts = await env.NewIngress().GetFactsAsync("fusion-line-01", null, 20, default);
        Assert.Equal(2, facts.Count); Assert.Contains(facts, x => x.BodyJson == first.BodyJson && x.SourceSequence == first.SourceSequence);
        Assert.Contains(facts, x => x.Kind == "source.counter-run" && x.ProjectionState == "complete");
    }
    /// <summary>编号与序号指向两条不同原文时仍为冲突；同一条同时命中两索引仍为重复。</summary>
    [Fact]
    public async Task ReceiptRejectsMixedUniqueIdentitiesWithoutDuplicatingIdenticalMatches() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var first = FusionIngressTestEnvironment.Fact("parcel.detected", 1);
        var second = FusionIngressTestEnvironment.Fact("parcel.detected", 2, "4");
        var stored = await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first, second), default);
        Assert.All(stored.Records, x => Assert.Equal("stored", x.Status));
        var mixed = first with { SourceSequence = second.SourceSequence };
        Assert.Equal("conflict", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, mixed), default)).Records).Status);
        Assert.Equal("duplicate", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first), default)).Records).Status);
        Assert.Equal(2, (await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).Count);
    }

    /// <summary>错误摘要、未知类型及内外身份不一致明确拒绝，不冒充成功存储。</summary>
    [Theory]
    [InlineData("hash")]
    [InlineData("kind")]
    [InlineData("source")]
    [InlineData("sequence")]
    public async Task InvalidFactIsRejected(string invalid) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var fact = FusionIngressTestEnvironment.Fact(invalid == "kind" ? "future.unknown" : "parcel.detected", 1, source: invalid == "source" ? "fusion-line-02" : "fusion-line-01");
        if (invalid == "hash") fact = fact with { BodySha256 = new string('0', 64) };
        if (invalid == "sequence") fact = fact with { SourceSequence = "2" };
        Assert.Equal("rejected", Assert.Single((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, fact), default)).Records).Status);
        Assert.Empty(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default));
    }
    /// <summary>异常先于检测保持未知检测时间，晚到检测重建实际时间，空测量不会覆盖先前数据。</summary>
    [Fact]
    public async Task OutOfOrderProjectionPreservesDetectionMeasurementAndLanding() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var detected = DateTimeOffset.Now.AddSeconds(-10).ToOffset(TimeSpan.Zero);
        var exception = FusionIngressTestEnvironment.Fact("parcel.exception", 1, data: new { exceptionType = "BlurryWaybill" }, time: detected.AddSeconds(1));
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, exception), default);
        Assert.Equal(1, await env.Projector().ProjectAsync(default));
        var stored = Assert.Single(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default));
        var id = long.Parse(stored.ParcelId!);
        Assert.Null((await env.Database.Parcels.GetByIdAsync(id, default))!.DetectedTime);
        var landed = FusionIngressTestEnvironment.Fact("parcel.landed", 4, data: new { actualLandingConfirmed = true, actualChute = "0007" }, time: detected.AddSeconds(5));
        var measurement = FusionIngressTestEnvironment.Fact("parcel.measurement", 3, data: new { barcode = "SAME-BARCODE", weightGrams = 1200m,
            lengthMm = 200m, widthMm = 100m, heightMm = 50m, volumetricWeightGrams = 888m }, time: detected.AddSeconds(2));
        var detection = FusionIngressTestEnvironment.Fact("parcel.detected", 2, time: detected);
        var partial = FusionIngressTestEnvironment.Fact("parcel.measurement", 5, data: new { barcode = "SAME-BARCODE", weightGrams = (decimal?)null }, time: detected.AddSeconds(3));
        Assert.All((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, landed, measurement, detection, partial), default)).Records, x => Assert.Equal("stored", x.Status));
        Assert.Equal(4, await env.Projector(env.NewIngress()).ProjectAsync(default));
        var parcel = (await env.Database.Parcels.GetByIdAsync(id, default))!;
        Assert.Equal(FusionProtocol.Local(detected, "Asia/Shanghai"), parcel.DetectedTime);
        Assert.Equal(1.2m, parcel.Weight); Assert.Equal(1000000m, parcel.Volume); Assert.Equal(888m, parcel.VolumetricWeightGrams);
        Assert.Equal("0007", parcel.ActualChuteCode); Assert.Equal(1, (int)parcel.Status);
    }
    /// <summary>同条码、不同来源或计数周期独立建包，设备长编号保持精度。</summary>
    [Fact]
    public async Task ParcelIdentityDoesNotUseBarcodeJournalOrProducerSession() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var first = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var second = await env.Ingress.RegisterAsync("b", "fusion-line-02", FusionIngressTestEnvironment.Hello("fusion-line-02"), default);
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(first,
            FusionIngressTestEnvironment.Fact("parcel.detected", 1),
            FusionIngressTestEnvironment.Fact("parcel.detected", 2, run: "44444444444444444444444444444444")), default);
        await env.Ingress.PublishAsync("b", FusionIngressTestEnvironment.Batch(second,
            FusionIngressTestEnvironment.Fact("parcel.detected", 1, source: "fusion-line-02")), default);
        Assert.Equal(3, await env.Projector().ProjectAsync(default));
        var facts = (await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).Concat(await env.Ingress.GetFactsAsync("fusion-line-02", null, 20, default));
        Assert.Equal(3, facts.Select(x => x.ParcelId).Distinct().Count());
        foreach (var item in facts) Assert.Equal(9007199254740993, (await env.Database.Parcels.GetByIdAsync(long.Parse(item.ParcelId!), default))!.SourceParcelId);
    }
    /// <summary>Provider 的 HTTP 成功和 completed 不能代替业务接受，实际落格才完成包裹。</summary>
    [Fact]
    public async Task TransportAndOperationCompletionDoNotProveBusinessAcceptance() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var frames = new[] { FusionIngressTestEnvironment.Fact("parcel.detected", 1),
            FusionIngressTestEnvironment.Fact("provider.interaction", 2, data: new { statusCode = 200, outcome = "success", outcomeLevel = "transport" }),
            FusionIngressTestEnvironment.Fact("provider.operation", 3, data: new { operation = "landing", status = "completed" }),
            FusionIngressTestEnvironment.Fact("parcel.session-ended", 4, data: new { endReason = "SessionExpired", actualLandingConfirmed = false }) };
        Assert.All((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, frames), default)).Records, x => Assert.Equal("stored", x.Status));
        Assert.Equal(3, await env.Projector().ProjectAsync(default));
        var id = (await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).First(x => x.ParcelId is not null).ParcelId!;
        var parcel = (await env.Database.Parcels.GetByIdAsync(long.Parse(id), default))!;
        Assert.Equal(0, (int)parcel.Status); Assert.Equal(0, (int)parcel.RequestStatus); Assert.Null(parcel.CompletedTime);
    }
    /// <summary>来源业务投影失败仍保留已经确认的原文，重建服务后恢复同一幂等任务。</summary>
    [Fact]
    public async Task FailedProjectionSurvivesReceiverRestart() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, FusionIngressTestEnvironment.Fact("parcel.detected", 1)), default);
        env.Database.Failure.FailNextProcessingCommit = true;
        Assert.Equal(0, await env.Projector().ProjectAsync(default));
        await using (var db = await env.Database.Factory.CreateDbContextAsync()) {
            var row = await db.Set<FusionFactReceipt>().AsTracking().SingleAsync(); Assert.Equal("retry", row.ProjectionState);
            row.NextProjectionAt = DateTime.Now.AddMinutes(-1); await db.SaveChangesAsync();
        }
        Assert.Equal(1, await env.Projector(env.NewIngress()).ProjectAsync(default));
        Assert.Equal("complete", Assert.Single(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).ProjectionState);
    }
    /// <summary>原始 DWS 与拒绝绑定保持未关联，不按相同条码匹配其他包裹。</summary>
    [Fact]
    public async Task UnboundDwsAndRejectedBindingRemainIndependent() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var records = new[] { FusionIngressTestEnvironment.Fact("dws.received", 1, null, new { barcode = "SAME-BARCODE", weightGrams = 1500m }),
            FusionIngressTestEnvironment.Fact("dws.binding", 2, data: new { decision = "Rejected", candidateParcelId = "9007199254740993", finalParcelId = (string?)null }) };
        Assert.All((await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, records), default)).Records, x => Assert.Equal("stored", x.Status));
        Assert.Equal(2, await env.Projector().ProjectAsync(default));
        Assert.All(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default), x => Assert.Null(x.ParcelId));
        Assert.Equal(2, (await env.Database.Processing.GetUnboundAsync(20, default)).Count);
    }
    /// <summary>心跳在线和数据舍弃证据持久化；不同来源和发送库彼此独立。</summary>
    [Fact]
    public async Task HeartbeatCountersPersistAndCannotRegress() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var heartbeat = new FusionHeartbeat(lease.SourceInstanceId, lease.JournalId, lease.LeaseId, DateTimeOffset.Now.ToOffset(TimeSpan.Zero), 20, 1, 2, 3, 4, true, 1024);
        await env.Ingress.HeartbeatAsync("a", heartbeat, default);
        var source = (await env.NewIngress().GetSourcesAsync(default)).Single(x => x.SourceInstanceId == lease.SourceInstanceId);
        Assert.True(source.IsOnline); Assert.Equal(3, source.DroppedUnacknowledgedFacts); Assert.Equal(4, source.DroppedUnacknowledgedImages);
        await Assert.ThrowsAsync<ArgumentException>(() => env.Ingress.HeartbeatAsync("a", heartbeat with { DroppedUnacknowledgedFacts = 2 }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.Ingress.HeartbeatAsync("a", heartbeat with { SourceInstanceId = "fusion-line-02" }, default));
        await env.Ingress.DisconnectAsync("a", default);
        Assert.False((await env.NewIngress().GetSourcesAsync(default)).Single(x => x.SourceInstanceId == lease.SourceInstanceId).IsOnline);
        Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, JsonSerializer.Serialize(source));
    }
}
