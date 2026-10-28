using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>历史时间可靠性在数据库读取时核对，原始事实及已保存耗时不被隐式改写。</summary>
public sealed class ParcelTimestampReliabilityReadTests {
    /// <summary>可靠历史完成样本按耗时倒序返回的固定编号。</summary>
    private static readonly string[] ReliableCompletionIds = ["4", "1"];

    /// <summary>基础表和物理分表中的不可靠历史耗时从平均及完成分析排除，包裹数量与历史原值保持不变。</summary>
    [Fact]
    public async Task HistoricalUnreliableEndpointsDoNotEnterLifecycleReportsOrRewriteStorage() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await SeedLegacyAsync(database, 1, at, at, 10, null, null);
        await SeedLegacyAsync(database, 2, at, at, 20, false, true, baseTable: true);
        await SeedLegacyAsync(database, 3, at, at, 30, true, false);
        await SeedLegacyAsync(database, 4, at, at, 40, true, true, baseTable: true);

        var overview = await Analytics(database).GetAsync(at.Date, at.Date, default);
        Assert.Equal(4, overview.DetectedCount);
        Assert.Equal(4, overview.CompletedCount);
        Assert.Equal(25m, overview.AverageLifecycleSeconds);
        Assert.Equal(2, Assert.Single(overview.Daily).LifecycleSampleCount);
        Assert.Equal(25m, overview.Daily[0].AverageLifecycleSeconds);

        var analysis = await Analysis(database).ReadAsync(new ParcelAnalysisRequest {
            View = "duration", DurationType = "completion", FromDate = at.Date, ToDate = at.Date
        }, default);
        Assert.Equal(4, analysis.ParcelCount);
        Assert.Equal(4, analysis.CompletedCount);
        Assert.Equal(2, analysis.LifecycleSampleCount);
        Assert.Equal(25000m, analysis.AverageMilliseconds);
        Assert.Equal(ReliableCompletionIds, analysis.Items.Select(item => item.Id));

        await using var db = await database.Factory.CreateDbContextAsync();
        var suffixes = (await database.Partitions.GetReadCatalogAsync(default)).Suffixes;
        await using var read = ParcelPartitionReadContext<ParcelAnalysisSnapshot>.Create<Parcel>(db, suffixes);
        var saved = await read.QueryAll(suffixes, rows => rows).OrderBy(parcel => parcel.Id)
            .Select(parcel => parcel.LifecycleMilliseconds).ToArrayAsync();
        Assert.Equal(new long?[] { 10000, 20000, 30000, 40000 }, saved);
    }

    /// <summary>只核对所用端点及完整来源身份，其他时间、阶段、失败完成尝试和外来事实不影响有效耗时。</summary>
    [Fact]
    public async Task UnrelatedUnreliableFactsDoNotDiscardReliableEndpoints() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var unrelated = new[] {
            Fact(1, "other-stage", ParcelProcessingStage.ScanUploaded, at.AddSeconds(1), at) with { HasReliableTimestamp = false },
            Fact(1, "other-detection", ParcelProcessingStage.Detected, at.AddSeconds(2), at) with { HasReliableTimestamp = false },
            Fact(1, "later-same-detection", ParcelProcessingStage.Detected, at, at) with { HasReliableTimestamp = false },
            Fact(1, "earlier-completion", ParcelProcessingStage.SortingCompleted, at.AddSeconds(3), at) with { HasReliableTimestamp = false },
            Fact(1, "before-same-completion", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10), at) with { HasReliableTimestamp = false },
            Fact(1, "failed-completion", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10), at) with { HasReliableTimestamp = false, IsSuccess = false },
            Fact(1, "foreign-instance", ParcelProcessingStage.Detected, at, at) with { SourceInstanceId = "TIMESTAMP-READ", HasReliableTimestamp = false },
            Fact(1, "foreign-run", ParcelProcessingStage.Detected, at, at) with { SourceRunId = "different-run", HasReliableTimestamp = false },
            Fact(1, "foreign-parcel", ParcelProcessingStage.Detected, at, at) with { SourceParcelId = 999, HasReliableTimestamp = false }
        };
        await SeedLegacyAsync(database, 1, at, at, 10, true, true, additional: unrelated);

        var overview = await Analytics(database).GetAsync(at.Date, at.Date, default);
        Assert.Equal(10m, overview.AverageLifecycleSeconds);
        Assert.Equal(1, Assert.Single(overview.Daily).LifecycleSampleCount);
        var analysis = await Analysis(database).ReadAsync(new ParcelAnalysisRequest {
            View = "duration", DurationType = "completion", FromDate = at.Date, ToDate = at.Date
        }, default);
        Assert.Equal(1, analysis.LifecycleSampleCount);
        Assert.Equal(10000m, analysis.AverageMilliseconds);
    }

    /// <summary>源端检测节拍排除明确不可靠检测，仍接受未知标记及不可靠完成，且补传分表不被检测日期裁剪。</summary>
    [Fact]
    public async Task DetectionIntervalsExcludeOnlyExplicitlyUnreliableDetectionEndpoints() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var registeredAt = at.AddDays(2);
        await SeedLegacyAsync(database, 1, at, registeredAt, 10, null, true);
        await SeedLegacyAsync(database, 2, at.AddMilliseconds(100), registeredAt, 10, false, true, additional: [
            Fact(2, "later-same-detection", ParcelProcessingStage.Detected, at.AddMilliseconds(100), registeredAt) with { HasReliableTimestamp = true }
        ]);
        await SeedLegacyAsync(database, 3, at.AddMilliseconds(500), registeredAt, 10, true, true, baseTable: true, additional: [
            Fact(3, "later-same-detection", ParcelProcessingStage.Detected, at.AddMilliseconds(500), registeredAt) with { HasReliableTimestamp = false }
        ]);
        await SeedLegacyAsync(database, 4, at.AddMilliseconds(1000), registeredAt, 10, null, false);

        var report = await Analytics(database).GetAsync(at.Date, at.Date, default);
        Assert.Equal(0, report.DetectedCount);
        Assert.Equal(2, report.CreationIntervalSampleCount);
        Assert.Equal(500m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(500m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(7200m, report.ActualSortingThroughputPerHour);
        Assert.Equal(7200m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>四库都必须能组合带端点核对的窄查询及分表聚合，生成SQL不会连接业务数据库。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void HistoricalEndpointFilterComposesOnEveryProvider(string provider) {
        // 离线四库上下文不占用进程级生产服务缓存，避免测试顺序触发提供器数量告警。
        var options = new DbContextOptionsBuilder<SortingHubDbContext>().EnableServiceProviderCaching(false);
        if (provider == "MySql") options.UseMySql("Server=127.0.0.1;Database=design_time_only;User Id=design_time_only", new MySqlServerVersion(new Version(8, 4, 0)));
        else if (provider == "SqlServer") options.UseSqlServer("Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True");
        else AdditionalDbContextOptions.Configure(options, provider, provider == "Oracle"
            ? "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1"
            : "Data Source=timestamp-read-design-time-only.db");
        using var db = new SortingHubDbContext(options.Options);
        using var read = ParcelPartitionReadContext<ParcelAnalysisSnapshot>.Create<Parcel>(db, ["", "202610"]);
        var at = new DateTime(2026, 10, 1);
        var sql = read.Query(["", "202610"], nameof(Parcel.CreatedTime), at, at.AddDays(1), false, requireReliableLifecycle: true)
            .GroupBy(parcel => parcel.CreatedTime.Date).Select(group => new { Date = group.Key, Count = group.LongCount(), Sum = group.Sum(parcel => parcel.LifecycleMilliseconds) })
            .ToQueryString();
        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Parcel_ProcessingRecords_202610", sql, StringComparison.Ordinal);
        Assert.Contains("HasReliableTimestamp", sql, StringComparison.Ordinal);
    }

    /// <summary>模拟旧版本已经保存耗时的快照，原始事实仍保留真实可靠性标记。</summary>
    private static async Task SeedLegacyAsync(RelationalParcelTestDatabase database, long id, DateTime detectedAt, DateTime registeredAt,
        int durationSeconds, bool? detectionReliable, bool? completionReliable, bool baseTable = false, ParcelProcessingRecord[]? additional = null) {
        var detected = Fact(id, "detected", ParcelProcessingStage.Detected, detectedAt, registeredAt) with { HasReliableTimestamp = detectionReliable };
        var completed = Fact(id, "completed", ParcelProcessingStage.SortingCompleted, detectedAt.AddSeconds(durationSeconds), registeredAt) with { HasReliableTimestamp = completionReliable };
        var facts = new[] { detected, completed }.Concat(additional ?? []).ToArray();
        var parcel = Parcel.CreateDetected(id, detected, registeredAt);
        parcel.ApplyProcessingRecords(facts.Select(fact => fact with { HasReliableTimestamp = true }).ToArray());
        Assert.Equal(durationSeconds * 1000L, parcel.LifecycleMilliseconds);
        var period = database.Partitions.Resolve(registeredAt);
        if (!baseTable) await database.Partitions.EnsureCreatedAsync(period, default);
        await using var db = baseTable ? await database.Factory.CreateDbContextAsync() : await database.Partitions.CreateContextAsync(period.Suffix, default);
        db.Add(parcel);
        db.AddRange(facts);
        await db.SaveChangesAsync();
    }

    /// <summary>来源身份及分表锚点稳定，完成阶段包含真实目标与实际格口。</summary>
    private static ParcelProcessingRecord Fact(long id, string name, ParcelProcessingStage stage, DateTime at, DateTime registeredAt) => new() {
        Key = id + ":" + name, RecordId = id + ":" + name, ParcelId = id, SourceParcelId = id,
        SourceInstanceId = "timestamp-read", SourceRunId = "run-a", Stage = stage, OccurredAt = at,
        RecordedAt = registeredAt, PartitionTime = registeredAt, PayloadHash = name,
        IsSuccess = stage == ParcelProcessingStage.SortingCompleted ? true : null,
        TargetChuteCode = stage == ParcelProcessingStage.SortingCompleted ? "01" : null,
        ActualChuteCode = stage == ParcelProcessingStage.SortingCompleted ? "01" : null
    };

    /// <summary>沿用实际报表预算和物理分表查询。</summary>
    private static ParcelAnalyticsReadService Analytics(RelationalParcelTestDatabase database) => new(database.Factory, Budget(), database.Partitions);

    /// <summary>完成耗时分析使用同一数据库及窗口预算。</summary>
    private static ParcelAnalysisReadService Analysis(RelationalParcelTestDatabase database) => new(database.Factory, Budget(), database.Partitions);

    /// <summary>独立测试使用默认报表预算，不截断任何事实总体。</summary>
    private static ReportingQueryBudgetPlanner Budget() => new(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions()));
}
