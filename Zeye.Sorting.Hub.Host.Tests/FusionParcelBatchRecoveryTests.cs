using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>单个投影输入损坏时不能阻止同票其他有效事实。</summary>
public sealed class FusionParcelBatchRecoveryTests {
    /// <summary>验证失败的批次退回逐条，坏事实重试，有效事实仍正常提交。</summary>
    [Fact]
    public async Task InvalidMappedItemDoesNotBlockValidItemsInSameParcelGroup() {
        await using var env = new FusionIngressTestEnvironment();
        await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease,
            FusionIngressTestEnvironment.Fact("parcel.detected", 1, "1"),
            FusionIngressTestEnvironment.Fact("parcel.detected", 2, "1")), default);
        await using (var db = await env.Database.Factory.CreateDbContextAsync()) {
            var row = await db.Set<FusionFactReceipt>().AsTracking().SingleAsync(item => item.SourceSequence == 1);
            var mapped = JsonNode.Parse(row.ProjectionJson!)!;
            mapped["stage"] = 999;
            row.ProjectionJson = mapped.ToJsonString();
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, await env.Projector().ProjectAsync(default));
        var facts = await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default);
        Assert.Equal("retry", Assert.Single(facts, fact => fact.SourceSequence == "1").ProjectionState);
        Assert.Equal("complete", Assert.Single(facts, fact => fact.SourceSequence == "2").ProjectionState);
        Assert.Equal(1, await env.Database.CountPhysicalAsync("Parcels_" + env.Database.Partitions.Resolve(DateTime.Now).Suffix));
    }
}
