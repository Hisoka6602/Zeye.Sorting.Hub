using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>批量投影状态的认领隔离、独立归属、失败重试和过期认领保护。</summary>
public sealed class FusionProjectionBatchTests {
    /// <summary>多个接收实例不能重复认领同一批，认领顺序仍对应原始序号。</summary>
    [Fact]
    public async Task BatchClaimsRemainExclusiveAndOrdered() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var facts = Enumerable.Range(1, 3).Select(index => FusionIngressTestEnvironment.Fact("parcel.detected", index, index.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, facts), default);
        var first = await env.Ingress.ClaimProjectionsAsync(default);
        Assert.Equal(3, first.Count); Assert.Single(first.Select(item => item.ClaimId).Distinct());
        Assert.Equal(new long?[] { 1, 2, 3 }, first.Select(item => item.Request.SourceParcelId));
        Assert.Empty(await env.NewIngress().ClaimProjectionsAsync(default));
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.All(await db.Set<FusionFactReceipt>().ToListAsync(), row => Assert.Equal(1, row.ProjectionAttempts));
    }

    /// <summary>成功、未关联与失败凭据分别保留归属和状态，失败不会抹掉已确认原文。</summary>
    [Fact]
    public async Task BatchCompletionPreservesEachParcelAndRetryError() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var facts = new[] { FusionIngressTestEnvironment.Fact("parcel.detected", 1, "1"),
            FusionIngressTestEnvironment.Fact("parcel.detected", 2, "2"), FusionIngressTestEnvironment.Fact("dws.received", 3, null),
            FusionIngressTestEnvironment.Fact("parcel.detected", 4, "4") };
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, facts), default);
        var items = await env.Ingress.ClaimProjectionsAsync(default);
        await env.Ingress.FinishProjectionsAsync([(items[0], "101", null), (items[1], "202", null), (items[2], null, null),
            (items[3], null, "SimulatedUnavailable")], default);
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        var stored = (await db.Set<FusionFactReceipt>().ToListAsync()).ToDictionary(row => row.Key);
        Assert.Equal("101", stored[items[0].Key].ParcelId); Assert.Equal("202", stored[items[1].Key].ParcelId);
        Assert.Null(stored[items[2].Key].ParcelId);
        foreach (var item in items.Take(3)) { Assert.Equal("complete", stored[item.Key].ProjectionState); Assert.Null(stored[item.Key].ProjectionClaimId); }
        var retry = stored[items[3].Key]; Assert.Equal("retry", retry.ProjectionState); Assert.Equal("SimulatedUnavailable", retry.ProjectionError);
        Assert.True(retry.NextProjectionAt > DateTime.Now); Assert.Null(retry.ProjectionClaimId);
        Assert.All(facts, fact => Assert.Contains(stored.Values, row => row.BodyJson == fact.BodyJson && row.BodySha256 == fact.BodySha256));
        Assert.Empty(await env.NewIngress().ClaimProjectionsAsync(default));
    }

    /// <summary>过期工作者不能覆盖后来认领者；新认领可完成同一不可变凭据。</summary>
    [Fact]
    public async Task StaleBatchCannotCompleteRenewedClaims() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, FusionIngressTestEnvironment.Fact("parcel.detected", 1)), default);
        var old = Assert.Single(await env.Ingress.ClaimProjectionsAsync(default));
        await using (var db = await env.Database.Factory.CreateDbContextAsync()) {
            await db.Set<FusionFactReceipt>().ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ProjectionClaimUntil, DateTime.Now.AddMinutes(-1)));
        }
        var fresh = Assert.Single(await env.NewIngress().ClaimProjectionsAsync(default));
        Assert.NotEqual(old.ClaimId, fresh.ClaimId);
        await env.Ingress.FinishProjectionsAsync([(old, "obsolete", null)], default);
        Assert.Equal("pending", Assert.Single(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).ProjectionState);
        await env.Ingress.FinishProjectionsAsync([(fresh, "fresh", null)], default);
        var stored = Assert.Single(await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default));
        Assert.Equal("complete", stored.ProjectionState); Assert.Equal("fresh", stored.ParcelId);
    }
}
