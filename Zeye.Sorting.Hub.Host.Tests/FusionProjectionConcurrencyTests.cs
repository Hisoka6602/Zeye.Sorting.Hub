using Zeye.Sorting.Hub.Application.Services.Fusion;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>并发扩展不能使同票乱序，完成标记必须等待全部独立业务提交。</summary>
public sealed class FusionProjectionConcurrencyTests {
    /// <summary>用可控数据库等待模拟IO，验证超过八组可同时推进且同票仍顺序完成。</summary>
    [Fact]
    public async Task IndependentParcelsOverlapWhileEachParcelRemainsSequential() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("parallel", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var detected = Enumerable.Range(1, 20).Select(n => FusionIngressTestEnvironment.Fact("parcel.detected", n,
            n.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var measured = Enumerable.Range(1, 20).Select(n => FusionIngressTestEnvironment.Fact("parcel.measurement", n + 20,
            n.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var batch = FusionIngressTestEnvironment.Batch(lease, detected.Concat(measured).ToArray());
        Assert.All((await env.Ingress.PublishAsync("parallel", batch, default)).Records, row => Assert.Equal("stored", row.Status));
        var repository = new FusionOverlappingProjectionRepository();
        var projector = new FusionProjectionService(env.Ingress, new ParcelProcessingApplicationService(repository));
        Assert.Equal(40, await projector.ProjectAsync(default));
        Assert.Equal(20, repository.Peak);
        Assert.Equal(40, repository.Writes.Count);
        foreach (var number in Enumerable.Range(1, 20)) {
            var writes = repository.Writes.Where(row => row.Parcel == number).Select(row => row.Stage).ToArray();
            Assert.Equal(new[] { ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound }, writes);
        }
        Assert.All(await env.Ingress.GetFactsAsync("fusion-line-01", null, 100, default), row => {
            Assert.Equal("complete", row.ProjectionState); Assert.Null(row.ProjectionError);
        });
    }

    /// <summary>非法并发度在领取任何数据库任务前被拒绝，避免无界或停滞的后台循环。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(33)]
    public async Task InvalidConcurrencyCannotClaimWork(int concurrency) {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var projector = new FusionProjectionService(env.Ingress, new ParcelProcessingApplicationService(new FusionOverlappingProjectionRepository()), concurrency);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => projector.ProjectAsync(default));
    }

}
