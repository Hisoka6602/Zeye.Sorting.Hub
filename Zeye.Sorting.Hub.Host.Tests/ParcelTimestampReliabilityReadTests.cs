using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
    /// <summary>查询计划及历史维护共用的端点索引列顺序。</summary>
    private static readonly string[] EndpointIndexColumns = [nameof(ParcelProcessingRecord.ParcelId), nameof(ParcelProcessingRecord.OccurredAt),
        nameof(ParcelProcessingRecord.Stage), nameof(ParcelProcessingRecord.HasReliableTimestamp)];

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

    /// <summary>按包裹、端点、阶段及可靠性定位事实，避免每票扫描全部阶段及大报文；基础表和历史分表均受保护。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LifecycleEndpointChecksUseAllColumnsOfReliabilityLookupIndex(bool baseTable) {
        var capture = new StatisticsCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerMonth", capture);
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        for (var id = 1; id <= 8; id++) {
            var unrelated = Enumerable.Range(0, 40).Select(index => Fact(id, "unrelated-" + index,
                ParcelProcessingStage.ScanUploaded, at.AddSeconds(index + 1), at) with {
                    HasReliableTimestamp = false, RawPayload = new string('x', 2048)
                }).ToArray();
            await SeedLegacyAsync(database, id, at, at, 10, true, true, baseTable, unrelated);
        }
        await using var db = await database.Factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlRawAsync("ANALYZE");
        var suffix = baseTable ? string.Empty : database.Partitions.Resolve(at).Suffix;
        await using var read = ParcelPartitionReadContext<ParcelAnalyticsSnapshot>.Create<Parcel>(db, [suffix]);
        var rows = await read.Query([suffix], nameof(Parcel.CreatedTime), at.Date, at.Date.AddDays(1), false,
                requireReliableLifecycle: true)
            .GroupBy(parcel => parcel.CreatedTime.Date)
            .Select(group => new { Count = group.LongCount(), Sum = group.Sum(parcel => parcel.LifecycleMilliseconds!.Value) })
            .ToListAsync();
        Assert.Equal(8, Assert.Single(rows).Count);
        Assert.Equal(80000, rows[0].Sum);
        var captured = Assert.Single(capture.Commands);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + captured.Sql;
        foreach (var (name, value, type) in captured.Parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name; parameter.Value = value; parameter.DbType = type;
            command.Parameters.Add(parameter);
        }
        var lookups = new List<string>();
        await using var plan = await command.ExecuteReaderAsync();
        while (await plan.ReadAsync()) {
            var detail = plan.GetString(3);
            if (detail.StartsWith("SEARCH f ", StringComparison.Ordinal)) lookups.Add(detail);
            Assert.False(detail.StartsWith("SCAN f", StringComparison.Ordinal), detail);
        }
        Assert.NotEmpty(lookups);
        Assert.All(lookups, detail => Assert.Contains("ParcelId=? AND OccurredAt=? AND Stage=? AND HasReliableTimestamp=?",
            detail, StringComparison.Ordinal));
    }

    /// <summary>同刻重试按尝试号再按编号排序，检测取首个、完成取最后未失败事实，可靠性不能因查询拆分而变化。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameTimestampRetryOrderPreservesSelectedEndpointReliability(bool baseTable) {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await SeedLegacyAsync(database, 1, at, at, 10, true, true, baseTable, [
            Fact(1, "000-later-detection", ParcelProcessingStage.Detected, at, at) with { AttemptNumber = 2, HasReliableTimestamp = false },
            Fact(1, "000-earlier-completion", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10), at) with { HasReliableTimestamp = false },
            Fact(1, "failed-last-completion", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10), at) with { AttemptNumber = 3, HasReliableTimestamp = false, IsSuccess = false }
        ]);
        await SeedLegacyAsync(database, 2, at.AddMilliseconds(100), at, 20, false, true, baseTable, [
            Fact(2, "000-later-detection", ParcelProcessingStage.Detected, at.AddMilliseconds(100), at) with { AttemptNumber = 2, HasReliableTimestamp = true }
        ]);
        await SeedLegacyAsync(database, 3, at.AddMilliseconds(500), at, 30, true, true, baseTable, [
            Fact(3, "000-later-completion", ParcelProcessingStage.SortingCompleted, at.AddMilliseconds(500).AddSeconds(30), at) with { AttemptNumber = 2, HasReliableTimestamp = false }
        ]);

        var report = await Analytics(database).GetAsync(at.Date, at.Date, default);
        Assert.Equal(3, report.CompletedCount);
        Assert.Equal(1, Assert.Single(report.Daily).LifecycleSampleCount);
        Assert.Equal(10m, report.AverageLifecycleSeconds);
        Assert.Equal(1, report.CreationIntervalSampleCount);
        Assert.Equal(500m, report.MedianCreationIntervalMilliseconds);
    }

    /// <summary>升级时已登记的历史事实分表自动补齐端点索引，不要求清空或重写旧业务数据。</summary>
    [Fact]
    public async Task MaintenanceRepairsMissingHistoricalEndpointIndexWithoutChangingFacts() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 1, 2, 10, 0, 0);
        await SeedLegacyAsync(database, 1, at, at, 10, true, true);
        var period = database.Partitions.Resolve(at);
        await using var db = await database.Partitions.CreateContextAsync(period.Suffix, default);
        var index = db.Model.FindEntityType(typeof(ParcelProcessingRecord))!.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(EndpointIndexColumns));
        var name = index.GetDatabaseName()!;
        await db.Database.ExecuteSqlRawAsync("DROP INDEX " + db.GetService<ISqlGenerationHelper>().DelimitIdentifier(name));
        var maintenance = new PartitionMaintenanceService(database.Partitions, database.Factory,
            Microsoft.Extensions.Options.Options.Create(new ShardingPrebuildOptions()));
        await maintenance.ExecuteAsync(default);
        Assert.Equal(1, await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {0}", name).SingleAsync());
        Assert.Equal(2, await db.Set<ParcelProcessingRecord>().CountAsync());
        Assert.Equal(10000, await db.Set<Parcel>().Select(parcel => parcel.LifecycleMilliseconds).SingleAsync());
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
            ? "User Id=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1"
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
        if (provider == "MySql") {
            Assert.DoesNotContain("SELECT DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("f FORCE INDEX (`IX_Processing_Endpoint_Reliability_Covering`)", sql, StringComparison.Ordinal);
            Assert.Contains("e FORCE INDEX (`IX_Processing_Endpoint_Reliability_Covering`)", sql, StringComparison.Ordinal);
            Assert.Contains("f FORCE INDEX (`IX_Processing_Endpoint_Reliability_Covering_202610`)", sql, StringComparison.Ordinal);
            Assert.Contains("e FORCE INDEX (`IX_Processing_Endpoint_Reliability_Covering_202610`)", sql, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(ParcelProcessingRecord.RawPayload), sql, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(ParcelProcessingRecord.RequestBody), sql, StringComparison.Ordinal);
        } else Assert.DoesNotContain("FORCE INDEX", sql, StringComparison.Ordinal);
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
