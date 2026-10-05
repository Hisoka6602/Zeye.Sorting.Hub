using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TestOptions = Microsoft.Extensions.Options.Options;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>统计查询的大字段隔离、边界、并发口径及历史索引自动修复回归。</summary>
public sealed class ParcelStatisticsQueryTests {
    /// <summary>跨表SQL只包含统计所需列，时间条件在每个分支内参数化，不下载巨大Provider原文。</summary>
    [Fact]
    public async Task StatisticsAvoidLargePayloadsAndKeepLateFactsInOlderPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var first = new DateTime(2026, 9, 28, 10, 0, 0);
        var detected = new ParcelProcessingRecord {
            RecordId = "statistics-detected", SourceInstanceId = "statistics-source", SourceRunId = "run-1", SourceParcelId = 1,
            Stage = ParcelProcessingStage.Detected, OccurredAt = first, RecordedAt = first, PartitionTime = first,
            PayloadHash = "statistics-detected", RawPayload = new string('x', 128 * 1024)
        };
        Assert.True((await database.Processing.AppendAsync(detected, default)).IsSuccess);
        var late = detected with {
            RecordId = "statistics-late", PayloadHash = "statistics-late", Stage = ParcelProcessingStage.ScanUploaded,
            OccurredAt = first.AddDays(2), IsSuccess = false
        };
        Assert.True((await database.Processing.AppendAsync(late, default)).IsSuccess);
        await using var db = await database.Factory.CreateDbContextAsync();
        var suffixes = await database.Partitions.GetReadSuffixesAsync(default);
        var query = ParcelPartitionQueryBuilder.BuildTimeRangeReadModel<ParcelProcessingRecord, ParcelProcessingStatisticsRow>(db,
            suffixes, nameof(ParcelProcessingRecord.OccurredAt), late.OccurredAt.Date, late.OccurredAt.Date.AddDays(1));
        var sql = query.ToQueryString();
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.RawPayload), sql);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.ResponseBody), sql);
        Assert.Equal(suffixes.Count, sql.Split("WHERE \"OccurredAt\" >=", StringSplitOptions.None).Length - 1);
        Assert.Contains("@p0", sql); Assert.Contains("@p1", sql);
        var row = Assert.Single(await query.ToListAsync());
        Assert.False(row.IsSuccess);
        Assert.Equal(ParcelProcessingStage.ScanUploaded, row.Stage);
        Assert.NotNull(row.ParcelId);
    }

    /// <summary>多个日期窗口同时读取不会共享DbContext，也不会串用统计结果。</summary>
    [Fact]
    public async Task ConcurrentReportsKeepTheirOwnDateWindowAndExactCounts() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var first = new DateTime(2026, 9, 28, 10, 0, 0);
        foreach (var day in Enumerable.Range(0, 2)) {
            var fact = new ParcelProcessingRecord {
                RecordId = "concurrent-" + day, SourceInstanceId = "statistics-source", SourceRunId = "run-1", SourceParcelId = day + 1,
                Stage = ParcelProcessingStage.Detected, OccurredAt = first.AddDays(day), RecordedAt = first.AddDays(day),
                PartitionTime = first.AddDays(day), PayloadHash = "concurrent-" + day
            };
            Assert.True((await database.Processing.AppendAsync(fact, default)).IsSuccess);
        }
        var reader = new ParcelAnalyticsReadService(database.Factory,
            new ReportingQueryBudgetPlanner(TestOptions.Create(new ReadOnlyDatabaseOptions())));
        var tasks = Enumerable.Range(0, 12).Select(index => reader.GetAsync(first.Date,
            first.Date.AddDays(index % 2), default)).ToArray();
        var results = await Task.WhenAll(tasks);
        for (var index = 0; index < results.Length; index++) {
            Assert.Equal(1 + index % 2, results[index].DetectedCount);
            Assert.Equal(1 + index % 2, results[index].ProcessingEventCount);
        }
    }

    /// <summary>升级时旧周期的缺失覆盖索引也会自动补齐，已有业务记录保持不变。</summary>
    [Fact]
    public async Task MaintenanceRepairsHistoricalIndexesAndPreservesRecords() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var period = database.Partitions.Resolve(new DateTime(2026, 1, 2));
        await database.Partitions.EnsureCreatedAsync(period, default);
        var fact = new ParcelProcessingRecord {
            RecordId = "history-preserved", SourceInstanceId = "statistics-source", SourceRunId = "run-1", SourceParcelId = 1,
            Stage = ParcelProcessingStage.Detected, OccurredAt = period.Start.AddHours(10), RecordedAt = period.Start.AddHours(10),
            PartitionTime = period.Start.AddHours(10), PayloadHash = "history-preserved"
        };
        Assert.True((await database.Processing.AppendAsync(fact, default)).IsSuccess);
        await using var db = await database.Partitions.CreateContextAsync(period.Suffix, default);
        var indexName = await RemoveProcessingStatisticsIndexAsync(db);
        var maintenance = new PartitionMaintenanceService(database.Partitions, database.Factory, TestOptions.Create(new ShardingPrebuildOptions()));
        await maintenance.ExecuteAsync(default);
        Assert.Equal(1, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {0}", indexName).SingleAsync());
        Assert.Equal(1, await db.Set<ParcelPartitionCatalogEntry>().CountAsync(entry => entry.Suffix == period.Suffix));
        Assert.Equal(1, await db.Set<Parcel>().CountAsync());
        Assert.Equal(1, await db.Set<ParcelProcessingRecord>().CountAsync());
        // 再次维护没有重复DDL或目录写入。
        await maintenance.ExecuteAsync(default);
        Assert.Equal(1, await db.Set<ParcelPartitionCatalogEntry>().CountAsync(entry => entry.Suffix == period.Suffix));
    }

    /// <summary>关闭建表授权或启用预演时，历史索引修复不能执行DDL或写入新周期目录。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task HistoricalIndexRepairHonorsDdlGuards(bool allowCreation, bool dryRun) {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var period = database.Partitions.Resolve(new DateTime(2026, 1, 2));
        await database.Partitions.EnsureCreatedAsync(period, default);
        await using var db = await database.Partitions.CreateContextAsync(period.Suffix, default);
        var indexName = await RemoveProcessingStatisticsIndexAsync(db);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay",
            ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = allowCreation.ToString(),
            ["Persistence:Sharding:WriteRouting:DryRun"] = dryRun.ToString()
        }).Build();
        var guarded = new ParcelPartitionStore(database.Factory, configuration);
        var maintenance = new PartitionMaintenanceService(guarded, database.Factory, TestOptions.Create(new ShardingPrebuildOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.ExecuteAsync(default));
        Assert.Equal(0, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {0}", indexName).SingleAsync());
        Assert.Equal(1, await db.Set<ParcelPartitionCatalogEntry>().CountAsync());
    }

    /// <summary>仅在独立SQLite测试库内移除覆盖索引，复现升级前的历史分表。</summary>
    private static async Task<string> RemoveProcessingStatisticsIndexAsync(SortingHubDbContext db) {
        var entity = db.Model.FindEntityType(typeof(ParcelProcessingRecord))!;
        var index = entity.GetIndexes().Single(candidate => candidate.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { nameof(ParcelProcessingRecord.OccurredAt), nameof(ParcelProcessingRecord.IsSuccess),
                nameof(ParcelProcessingRecord.ParcelId), nameof(ParcelProcessingRecord.Stage) }));
        var indexName = index.GetDatabaseName()!;
        await db.Database.ExecuteSqlRawAsync("DROP INDEX " + db.GetService<ISqlGenerationHelper>().DelimitIdentifier(indexName));
        return indexName;
    }
}
