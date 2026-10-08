using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证耐久耗时窄投影的兼容性、原子提交、历史恢复以及四库 EF 翻译。</summary>
public sealed class ParcelDurationProjectionTests {
    /// <summary>新事实即使在历史游标之前也与窄索引一起保存，失败和重试不产生孤立或重复索引。</summary>
    [Fact]
    public async Task NewFactsAndDurationIndexCommitAtomicallyAcrossPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1);
        var first = Fact("first", at) with { Stage = ParcelProcessingStage.Detected };
        Assert.True((await database.Processing.AppendAsync(first, default)).IsSuccess);
        var call = Fact("call", at.AddDays(2));
        database.Failure.FailNextProcessingCommit = true;
        Assert.False((await database.Processing.AppendAsync(call, default)).IsSuccess);
        await using var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default);
        Assert.Empty(await db.Set<ParcelDurationFact>().ToListAsync());
        Assert.True((await database.Processing.AppendAsync(call, default)).IsSuccess);
        Assert.True((await database.Processing.AppendAsync(call, default)).IsSuccess);
        var row = Assert.Single(await db.Set<ParcelDurationFact>().ToListAsync());
        Assert.Equal(at, row.PartitionTime);
        Assert.Equal("scan-upload", row.Type);
        Assert.Equal("https://example.test/scan", row.RequestUrl);
        Assert.Equal(0, row.ElapsedMilliseconds);
        Assert.Null(row.IsSuccess);
        var backfill = new ParcelDurationBackfillService(database.Factory, database.Partitions);
        while (await backfill.RunBatchAsync(default) > 0) { }
        Assert.True((await database.Processing.AppendAsync(call with { RecordId = "earlier-key", Key = string.Empty }, default)).IsSuccess);
        Assert.Equal(2, await db.Set<ParcelDurationFact>().CountAsync());
    }

    /// <summary>批次 SQL 后失败会回滚索引和游标；新服务实例继续主键游标，不漏或重复旧记录。</summary>
    [Fact]
    public async Task HistoricalBackfillResumesOnlyCommittedBatches() {
        var failure = new DurationProjectionFailureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: failure);
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.AddRange(Enumerable.Range(0, 600).Select(index => Fact(index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), at)));
            await db.SaveChangesAsync();
            await db.Set<ParcelDurationFact>().ExecuteDeleteAsync();
        }
        var backfill = new ParcelDurationBackfillService(database.Factory, database.Partitions);
        Assert.Equal(512, await backfill.RunBatchAsync(default));
        failure.FailNext = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => backfill.RunBatchAsync(default));
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            Assert.Equal(512, await db.Set<ParcelDurationFact>().CountAsync());
            var state = Assert.Single(await db.Set<ParcelDurationBackfillState>().ToListAsync());
            Assert.Equal("0511", state.Cursor);
            Assert.False(state.Completed);
        }
        backfill = new(database.Factory, database.Partitions);
        Assert.Equal(88, await backfill.RunBatchAsync(default));
        Assert.Equal(0, await backfill.RunBatchAsync(default));
        await using var verify = await database.Factory.CreateDbContextAsync();
        Assert.Equal(600, await verify.Set<ParcelDurationFact>().CountAsync());
        Assert.True(Assert.Single(await verify.Set<ParcelDurationBackfillState>().ToListAsync()).Completed);
    }

    /// <summary>补齐完成后强制刷新不再查询原始宽事实，五种接口保持完全相同的统计结果。</summary>
    [Fact]
    public async Task CompletedProjectionKeepsReportsAndNeverReadsMessages() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1);
        var first = Fact("first", at) with { Stage = ParcelProcessingStage.Detected, Barcode = "TEST-1" };
        var parcel = Parcel.CreateDetected(1, first, at);
        parcel.ApplyProcessingRecords([first]);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(parcel);
            db.Add(first);
            db.AddRange(new[] { "scan-upload", "chute-assignment", "landing", "image-upload", "other" }.Select((type, index) =>
                Fact("call-" + index, at) with { RawPayload = JsonSerializer.Serialize(new { kind = "provider-call", category = type, name = "Provider", outcomeLevel = "transport" }) }));
            await db.SaveChangesAsync();
        }
        var reader = new ParcelAnalysisReadService(database.Factory,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), database.Partitions);
        var before = new Dictionary<string, ParcelDurationAnalysisResponse>();
        foreach (var type in new[] { "scan-upload", "chute-request", "landing-report", "image-upload", "other-api" })
            before[type] = (await reader.ReadAsync(Request(at, type), default)).DurationAnalysis!;
        var backfill = new ParcelDurationBackfillService(database.Factory, database.Partitions);
        while (await backfill.RunBatchAsync(default) > 0) { }
        var projection = new ParcelDurationCallProjectionService(database.Factory, database.Partitions);
        while (await projection.RunBatchAsync(default) > 0) { }
        capture.Commands.Clear();
        foreach (var (type, expected) in before) {
            var actual = (await reader.ReadAsync(Request(at, type), default)).DurationAnalysis!;
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual with { GeneratedAt = expected.GeneratedAt })));
        }
        Assert.Empty(capture.Commands);
    }

    /// <summary>四种提供器均能翻译主键补齐和无 LOB 的窄表查询，且模型与迁移一致。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void BackfillAndNarrowReadsTranslateForAllProviders(string provider) {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(config).AddSortingHubPersistence(config)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        var sql = ParcelDurationBackfillService.BuildBatch(db.Set<ParcelProcessingRecord>().AsNoTracking(), "ABCDEF").ToQueryString();
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.RequestBody), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.ResponseBody), sql, StringComparison.Ordinal);
        using var read = Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.ParcelPartitionReadContext<ParcelDurationFact>.Create<ParcelDurationFact>(db, ["202610", ""], streaming: true);
        var query = read.Query(["202610"], nameof(ParcelDurationFact.PartitionTime), new(2026, 10, 1), new(2026, 10, 9), false)
            .Where(row => row.ParcelId != null && row.SourceInstanceId == "source").ToQueryString();
        Assert.Contains("Parcel_DurationFacts_202610", query, StringComparison.Ordinal);
        Assert.DoesNotContain("RawPayload", query, StringComparison.Ordinal);
        Assert.DoesNotContain("ErrorMessage", query, StringComparison.Ordinal);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    /// <summary>构造只含明确来源耗时、没有假造业务接受的调用事实。</summary>
    private static ParcelProcessingRecord Fact(string key, DateTime at) => new() {
        Key = key, RecordId = key, ParcelId = 1, SourceInstanceId = "source", SourceRunId = "run", SourceParcelId = 1,
        Stage = ParcelProcessingStage.ScanUploaded, RecordedAt = at, OccurredAt = at, PartitionTime = at,
        PayloadHash = "hash", ElapsedMilliseconds = 0, RequestUrl = "https://user:password@example.test/scan?token=private#fragment",
        RawPayload = "{\"kind\":\"provider-call\",\"category\":\"scan-upload\",\"name\":\"Provider\",\"outcomeLevel\":\"transport\"}"
    };
    /// <summary>强制刷新验证请求没有依赖进程缓存。</summary>
    private static ParcelAnalysisRequest Request(DateTime at, string type) => new() {
        View = "duration", DurationType = type, FromDate = at.Date, ToDate = at.Date, RefreshDurationSnapshot = true
    };
}
