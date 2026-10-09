using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>批次事务的耐久确认、混合输入隔离与单一图片归属回归。</summary>
public sealed class FusionBatchPersistenceTests {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedStoredStoredDuplicateValues = new[] { "stored", "stored", "duplicate" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedDuplicateStoredRejectedConflictValues = new[] { "duplicate", "stored", "rejected", "conflict" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedStoredConflictStoredValues = new[] { "stored", "conflict", "stored" };

    /// <summary>验证批次耐久性、输入隔离及单次事务提交。</summary>
    [Fact]
    public async Task FiftyFactsCommitOnceAndReplayWithoutAnotherWrite() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var facts = Enumerable.Range(1, 50).Select(n => FusionIngressTestEnvironment.Fact("parcel.detected", n,
            n.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var batch = FusionIngressTestEnvironment.Batch(lease, facts);
        var first = await env.Ingress.PublishAsync("a", batch, default);
        Assert.All(first.Records, row => Assert.Equal("stored", row.Status));
        Assert.Equal(1, env.Database.Failure.FusionReceiptWrites);
        Assert.Equal(50, (await env.Ingress.GetFactsAsync("fusion-line-01", null, 100, default)).Count);
        var replay = await env.Ingress.PublishAsync("a", batch, default);
        Assert.All(replay.Records, row => Assert.Equal("duplicate", row.Status));
        Assert.Equal(1, env.Database.Failure.FusionReceiptWrites);
    }

    /// <summary>验证批次耐久性、输入隔离及单次事务提交。</summary>
    [Fact]
    public async Task FailureAfterSqlRollsBackWholeBatchBeforeAnyAcknowledgement() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var first = FusionIngressTestEnvironment.Fact("parcel.detected", 1, "1");
        var second = FusionIngressTestEnvironment.Fact("parcel.detected", 2, "2");
        var batch = FusionIngressTestEnvironment.Batch(lease, first, second, first);
        env.Database.Failure.FailNextFusionReceiptAfterSql = true;
        Assert.All((await env.Ingress.PublishAsync("a", batch, default)).Records, x => Assert.Equal("retryable", x.Status));
        Assert.Empty(await env.NewIngress().GetFactsAsync("fusion-line-01", null, 20, default));
        Assert.Empty(await env.NewIngress().ClaimProjectionsAsync(default));
        var retry = await env.Ingress.PublishAsync("a", batch, default);
        Assert.Equal(CachedStoredStoredDuplicateValues, retry.Records.Select(x => x.Status));
        Assert.Equal(2, (await env.NewIngress().GetFactsAsync("fusion-line-01", null, 20, default)).Count);
    }

    /// <summary>验证批次耐久性、输入隔离及单次事务提交。</summary>
    [Fact]
    public async Task InvalidAndConflictingRowsDoNotDiscardValidBatchMembers() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var first = FusionIngressTestEnvironment.Fact("parcel.detected", 1, "1");
        await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first), default);
        var changedJson = first.BodyJson.Replace("SAME-BARCODE", "DIFFERENT-BARCODE", StringComparison.Ordinal);
        var changed = first with { BodyJson = changedJson, BodySha256 = FusionProtocol.Hash(changedJson) };
        var next = FusionIngressTestEnvironment.Fact("parcel.detected", 2, "2");
        var invalid = FusionIngressTestEnvironment.Fact("parcel.detected", 3, "3") with { BodySha256 = new string('0', 64) };
        var mixed = await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first, next, invalid, changed), default);
        Assert.Equal(CachedDuplicateStoredRejectedConflictValues, mixed.Records.Select(x => x.Status));
        Assert.Equal(2, env.Database.Failure.FusionReceiptWrites);
        Assert.Equal(2, (await env.Ingress.GetFactsAsync("fusion-line-01", null, 20, default)).Count);
    }

    /// <summary>验证批次耐久性、输入隔离及单次事务提交。</summary>
    [Fact]
    public async Task TwoAssociationsCannotRetargetSameImageWithinOneBatch() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var lease = await env.Ingress.RegisterAsync("a", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var data = new { associationConfirmed = true, sourceImageId = "same-image", candidateCount = 1, cameraName = "top" };
        var first = FusionIngressTestEnvironment.Fact("image.association", 1, "1", data);
        var other = FusionIngressTestEnvironment.Fact("image.association", 2, "2", data);
        var healthy = FusionIngressTestEnvironment.Fact("parcel.detected", 3, "3");
        var receipt = await env.Ingress.PublishAsync("a", FusionIngressTestEnvironment.Batch(lease, first, other, healthy), default);
        Assert.Equal(CachedStoredConflictStoredValues, receipt.Records.Select(x => x.Status));
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.Equal(1L, (await db.Set<FusionImageUpload>().SingleAsync()).SourceParcelId);
        Assert.Equal(2, await db.Set<FusionFactReceipt>().CountAsync());
    }

    /// <summary>模拟生产重试策略的事务守卫，事务必须在策略作用域内提交且故障后仍可补传。</summary>
    [Fact]
    public async Task RetryingRelationalStrategyCommitsBatchAndRecoversRollback() {
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        await using var original = await env.Database.Factory.CreateDbContextAsync();
        var builder = new DbContextOptionsBuilder<SortingHubDbContext>().UseSqlite(
            original.Database.GetConnectionString(), sqlite => sqlite.ExecutionStrategy(deps => new FusionTransactionGuardStrategy(deps)))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).AddInterceptors(env.Database.Failure);
        var factory = new PooledDbContextFactory<SortingHubDbContext>(builder.Options);
        var ingress = new FusionIngestionService(factory, Microsoft.Extensions.Options.Options.Create(env.Options), env.Root);
        var lease = await ingress.RegisterAsync("retry", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var batch = FusionIngressTestEnvironment.Batch(lease,
            FusionIngressTestEnvironment.Fact("parcel.detected", 1, "1"),
            FusionIngressTestEnvironment.Fact("parcel.detected", 2, "2"));
        env.Database.Failure.FailNextFusionReceiptAfterSql = true;
        Assert.All((await ingress.PublishAsync("retry", batch, default)).Records, row => Assert.Equal("retryable", row.Status));
        Assert.Empty(await ingress.GetFactsAsync("fusion-line-01", null, 20, default));
        Assert.All((await ingress.PublishAsync("retry", batch, default)).Records, row => Assert.Equal("stored", row.Status));
        Assert.Equal(2, (await ingress.GetFactsAsync("fusion-line-01", null, 20, default)).Count);
        Assert.All((await ingress.PublishAsync("retry", batch, default)).Records, row => Assert.Equal("duplicate", row.Status));
    }

}
