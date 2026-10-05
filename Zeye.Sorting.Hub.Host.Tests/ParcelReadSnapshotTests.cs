using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>启动预热、热路径零配置读取、即时发布、多实例同步和失败隔离的关系数据库回归。</summary>
public sealed class ParcelReadSnapshotTests {
    /// <summary>多票真实事实写入保持事务持久化，不重复查询规则文档或已预建目录。</summary>
    [Fact]
    public async Task WarmProcessingDoesNotReloadRulesOrPartitionMetadata() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var start = new DateTime(2026, 10, 6, 10, 0, 0);
        await database.Partitions.EnsureCreatedAsync(database.Partitions.Resolve(start), default);
        using var refresh = new PersistenceReadSnapshotRefreshService(database.Factory, database.Partitions);
        await refresh.StartAsync(default);
        try {
            var catalogReads = database.MetadataIo.CatalogReads;
            var ruleReads = database.MetadataIo.RuleReads;
            foreach (var number in Enumerable.Range(1, 16)) {
                var identity = "snapshot-" + number;
                var fact = new ParcelProcessingRecord { RecordId = identity, PayloadHash = identity,
                    SourceInstanceId = "snapshot-source", SourceRunId = "snapshot-run", SourceParcelId = number,
                    Stage = ParcelProcessingStage.Detected, OccurredAt = start.AddMilliseconds(number),
                    RecordedAt = start.AddMilliseconds(number), PartitionTime = start.AddMilliseconds(number) };
                var result = await database.Processing.AppendAsync(fact, default);
                Assert.True(result.IsSuccess, result.ErrorMessage);
            }
            Assert.Equal(catalogReads, database.MetadataIo.CatalogReads);
            Assert.Equal(ruleReads, database.MetadataIo.RuleReads);
            Assert.Equal(1, ruleReads);
            Assert.Equal(16, await database.CountPhysicalAsync("Parcels_20261006"));
        }
        finally { await refresh.StopAsync(default); }
    }

    /// <summary>本实例新建立即出现在已加载目录，其他实例的创建通过后台刷新发现。</summary>
    [Fact]
    public async Task CatalogPublishesLocalCreationAndRefreshesOtherInstances() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var empty = await database.Partitions.GetReadCatalogAsync(default);
        Assert.Single(empty.Suffixes);
        var first = database.Partitions.Resolve(new DateTime(2026, 10, 6));
        await database.Partitions.EnsureCreatedAsync(first, default);
        Assert.Contains(first.Suffix, (await database.Partitions.GetReadSuffixesAsync(default)));
        Assert.Single(empty.Suffixes);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay",
            ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = "true",
            ["Persistence:Sharding:WriteRouting:DryRun"] = "false"
        }).Build();
        var other = new ParcelPartitionStore(database.Factory, configuration);
        var next = other.Resolve(first.End);
        await other.EnsureCreatedAsync(next, default);
        Assert.DoesNotContain(next.Suffix, await database.Partitions.GetReadSuffixesAsync(default));
        await database.Partitions.RefreshReadCatalogAsync(default);
        Assert.Contains(next.Suffix, await database.Partitions.GetReadSuffixesAsync(default));
        var reads = database.MetadataIo.CatalogReads;
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => database.Partitions.GetReadSuffixesAsync(default)));
        await database.Partitions.EnsureCreatedAsync(next, default);
        Assert.Equal(reads, database.MetadataIo.CatalogReads);
    }

    /// <summary>冷目录并发加载只发起一次查询，调用取消不会破坏已发布快照。</summary>
    [Fact]
    public async Task CatalogColdLoadIsSingleFlightAndHonorsCancellation() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var snapshots = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => database.Partitions.GetReadCatalogAsync(default)));
        Assert.Equal(1, database.MetadataIo.CatalogReads);
        Assert.All(snapshots, snapshot => Assert.Same(snapshots[0], snapshot));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => database.Partitions.GetReadCatalogAsync(cancellation.Token));
        Assert.Same(snapshots[0], await database.Partitions.GetReadCatalogAsync(default));
    }

    /// <summary>保存立即更新执行快照，版本冲突和晚到旧快照不能覆盖最新发布。</summary>
    [Fact]
    public async Task CommittedRulesPublishImmediatelyAndRejectOlderVersions() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var cache = ClassificationRuleSnapshotCache.For(database.Factory);
        await cache.GetAsync(default);
        var store = new ManagedDocumentService(database.Factory);
        var json = JsonSerializer.Serialize<ClassificationRule[]>([.. ClassificationRuleDefaults.Create(), Rule("已提交规则")]);
        var committed = await store.WriteAsync("rules-exception", json, 0, default);
        Assert.NotNull(committed);
        var reads = database.MetadataIo.RuleReads;
        Assert.Contains(await cache.GetAsync(default), rule => rule.Name == "已提交规则");
        var stale = JsonSerializer.Serialize<ClassificationRule[]>([.. ClassificationRuleDefaults.Create(), Rule("旧规则")]);
        Assert.Null(await store.WriteAsync("rules-exception", stale, 0, default));
        cache.Publish([new() { Key = "rules-exception", Json = stale, Revision = 0 }]);
        Assert.DoesNotContain(await cache.GetAsync(default), rule => rule.Name == "旧规则");
        Assert.Equal(reads, database.MetadataIo.RuleReads);
    }

    /// <summary>损坏的新配置刷新失败，处理仍使用之前已成功发布的配置。</summary>
    [Fact]
    public async Task FailedRefreshKeepsLastCommittedRulesAndCachesAreDatabaseScoped() {
        await using var database = new RelationalParcelTestDatabase();
        await using var separate = new RelationalParcelTestDatabase();
        await database.InitializeAsync(); await separate.InitializeAsync();
        var cache = ClassificationRuleSnapshotCache.For(database.Factory);
        var store = new ManagedDocumentService(database.Factory);
        await store.WriteAsync("rules-exception", JsonSerializer.Serialize<ClassificationRule[]>(
            [.. ClassificationRuleDefaults.Create(), Rule("数据库专属规则")]), 0, default);
        var before = await cache.GetAsync(default);
        var isolated = await ClassificationRuleSnapshotCache.For(separate.Factory).GetAsync(default);
        Assert.DoesNotContain(isolated, rule => rule.Name == "数据库专属规则");
        await using var db = await database.Factory.CreateDbContextAsync();
        var document = await db.Set<ManagedDocument>().AsTracking().SingleAsync(item => item.Key == "rules-exception");
        document.Json = "损坏的配置"; document.Revision++;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<JsonException>(() => cache.RefreshAsync(default));
        Assert.Same(before, await cache.GetAsync(default));
    }

    /// <summary>构造可执行的自定义规则，便于验证耐久版本与内存发布一致性。</summary>
    private static ClassificationRule Rule(string name) => new() { Id = 1, Name = name, Status = "已发布", ExceptionType = 11,
        Conditions = [new() { Field = "包裹重量", Operator = "大于", Value = "1", Unit = "kg" }] };
}
