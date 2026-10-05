using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>超出单次结果分块与认领限额的持久化回归。</summary>
public sealed class FusionProjectionChunkTests {
    /// <summary>多个结果分块保留跨来源独立编号及全部未关联事实。</summary>
    [Fact]
    public async Task CompletionAcrossChunksKeepsSourceAndUnboundIdentities() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var first = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var second = await env.Ingress.RegisterAsync("b", "fusion-line-02", FusionIngressTestEnvironment.Hello("fusion-line-02"), default);
        var detected = Enumerable.Range(1, 8).Select(index => FusionIngressTestEnvironment.Fact("parcel.detected", index,
            index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var unbound = Enumerable.Range(9, 8).Select(index => FusionIngressTestEnvironment.Fact("dws.received", index, null)).ToArray();
        var otherSource = Enumerable.Range(1, 8).Select(index => FusionIngressTestEnvironment.Fact("parcel.detected", index,
            index.ToString(System.Globalization.CultureInfo.InvariantCulture), source: "fusion-line-02")).ToArray();
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(first, detected.Concat(unbound).ToArray()), default);
        await env.Ingress.PublishAsync("b", FusionIngressTestEnvironment.Batch(second, otherSource), default);
        Assert.Equal(24, await env.Projector().ProjectAsync(default));
        var all = (await env.Ingress.GetFactsAsync("fusion-line-01", null, 50, default))
            .Concat(await env.Ingress.GetFactsAsync("fusion-line-02", null, 50, default)).ToArray();
        Assert.All(all, item => Assert.Equal("complete", item.ProjectionState));
        Assert.Equal(16, all.Where(item => item.Kind == "parcel.detected").Select(item => item.ParcelId).Distinct().Count());
        Assert.All(all.Where(item => item.Kind == "dws.received"), item => Assert.Null(item.ParcelId));
        foreach (var item in all.Where(item => item.ParcelId is not null)) {
            var parcel = await env.Database.Parcels.GetByIdAsync(long.Parse(item.ParcelId!, System.Globalization.CultureInfo.InvariantCulture), default);
            Assert.Equal(item.SourceInstanceId, parcel!.SourceInstanceId);
        }
    }

    /// <summary>最多认领50条，其他工作者可取得剩余事实且不会重复取得已认领键。</summary>
    [Fact]
    public async Task ClaimLimitLeavesRemainingKeysAvailable() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var fifty = Enumerable.Range(1, 50).Select(index => FusionIngressTestEnvironment.Fact("dws.received", index, null)).ToArray();
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, fifty), default);
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, FusionIngressTestEnvironment.Fact("dws.received", 51, null)), default);
        var first = await env.Ingress.ClaimProjectionsAsync(default);
        var second = await env.NewIngress().ClaimProjectionsAsync(default);
        Assert.Equal(50, first.Count); Assert.Single(second);
        Assert.Equal(51, first.Concat(second).Select(item => item.Key).Distinct().Count());
        Assert.Empty(await env.NewIngress().ClaimProjectionsAsync(default));
        await env.Ingress.FinishProjectionsAsync(first.Select(item => (item, (string?)null, (string?)null)).ToArray(), default);
        await env.Ingress.FinishProjectionsAsync(second.Select(item => (item, (string?)null, (string?)null)).ToArray(), default);
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.All(await db.Set<FusionFactReceipt>().ToListAsync(), item => { Assert.Equal("complete", item.ProjectionState); Assert.Null(item.ParcelId); Assert.Equal(1, item.ProjectionAttempts); });
    }
}
