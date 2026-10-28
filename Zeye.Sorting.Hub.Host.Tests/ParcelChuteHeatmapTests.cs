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
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>以真实跨日分表验证独立格口聚合、来源隔离、截断及按指标下钻。</summary>
public sealed class ParcelChuteHeatmapTests {
    /// <summary>流向返回一行时，格口票数仍覆盖全部流向；原始编码和未知信息不混淆。</summary>
    [Fact]
    public async Task HeatmapAggregatesFullChutesBeforeLimitsAndDrillFiltersAcrossPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var start = new DateTime(2026, 10, 1, 10, 0, 0);
        await AddAsync(database, 1, start, "source-a", "线1", "0013", "13", true);
        await AddAsync(database, 2, start.AddDays(1), "source-a", "线1", "X02", "13");
        await AddAsync(database, 3, start, "source-a", "线1", "13", "13");
        await AddAsync(database, 4, start, "source-a", "线1", " x01 ", "X01");
        await AddAsync(database, 5, start, "source-a", "线1", "13", null);
        await AddAsync(database, 6, start, "source-a", "线1", null, "13");
        await AddAsync(database, 7, start, "source-b", "线1", "13", "13", true);
        await AddAsync(database, 8, start, "source-a", "线2", "13", "13");
        await AddAsync(database, 9, start, "source-a", "线1", null, null);
        await AddAsync(database, 10, start.AddDays(2), "source-a", "线1", "13", "13");
        var request = new ParcelAnalysisRequest { View = "chutes", FromDate = start.Date, ToDate = start.Date.AddDays(1) };
        var report = await Reader(database).ReadAsync(request, default);
        var actual = report.ActualChuteHeatmap!;
        var target = report.TargetChuteHeatmap!;
        Assert.Equal(9, report.ParcelCount);
        Assert.Equal(7, actual.SampleCount);
        Assert.Equal(7, target.SampleCount);
        Assert.Equal(2, actual.MissingCodeCount);
        Assert.Equal(2, target.MissingCodeCount);
        Assert.Equal(actual.SampleCount, actual.Cells.Sum(cell => cell.Count));
        Assert.Equal(target.SampleCount, target.Cells.Sum(cell => cell.Count));
        Assert.Equal(2, actual.Cells.Sum(cell => cell.MismatchCount));
        Assert.Equal(2, target.Cells.Sum(cell => cell.FallbackCount));
        var busiest = Assert.Single(actual.Cells, cell => cell.SourceInstanceId == "source-a" && cell.WorkstationName == "线1" && cell.ChuteCode == "13");
        Assert.Equal(4, busiest.Count);
        Assert.Equal(2, busiest.MismatchCount);
        Assert.Equal(1, busiest.FallbackCount);
        Assert.Equal(3, actual.Cells.Count(cell => cell.ChuteCode == "13"));
        Assert.Contains(target.Cells, cell => cell.ChuteCode == "0013" && cell.MismatchCount == 1);
        Assert.Contains(target.Cells, cell => cell.ChuteCode == " x01 " && cell.MismatchCount == 0);
        Assert.False(actual.Truncated);

        var limited = await Reader(database, 1).ReadAsync(request, default);
        Assert.True(limited.ChuteRoutesTruncated);
        Assert.True(limited.ActualChuteHeatmap!.Truncated);
        Assert.True(limited.TargetChuteHeatmap!.Truncated);
        Assert.Equal(1, Assert.Single(limited.ChuteRoutes).Count);
        Assert.Equal(4, Assert.Single(limited.ActualChuteHeatmap.Cells).Count);
        Assert.Equal(2, Assert.Single(limited.TargetChuteHeatmap.Cells).Count);
        Assert.Equal(7, limited.ActualChuteHeatmap.SampleCount);
        Assert.Equal(2, limited.ActualChuteHeatmap.MissingCodeCount);

        var scoped = request with { SourceInstanceId = "source-a", WorkstationName = "线1", ActualChuteCode = "13" };
        var mismatch = await Reader(database).ReadAsync(scoped with { MismatchOnly = true }, default);
        Assert.Equal(2, mismatch.FilteredCount);
        var fallback = await Reader(database).ReadAsync(scoped with { FallbackOnly = true }, default);
        Assert.Equal(1, fallback.FilteredCount);
        Assert.Equal("0013", Assert.Single(fallback.Items).TargetChuteCode);
        Assert.Equal(7, fallback.ParcelCount);
        Assert.Equal(4, fallback.ActualChuteHeatmap!.Cells.Single(cell => cell.ChuteCode == "13").Count);
        var intersection = await Reader(database).ReadAsync(scoped with { FallbackOnly = true, MismatchOnly = true }, default);
        Assert.Equal(1, intersection.FilteredCount);
        var raw = await Reader(database).ReadAsync(scoped with { ActualChuteCode = null, TargetChuteCode = " x01 " }, default);
        Assert.Equal(" x01 ", Assert.Single(raw.Items).TargetChuteCode);
    }

    /// <summary>来源仅大小写不同时，同名工作台和格口仍分别统计并可精确下钻。</summary>
    [Fact]
    public async Task CaseDistinctSourcesKeepSeparateRoutesHeatmapsAndFilters() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await AddAsync(database, 1, at, "fusion-a", "共享工作台", "01", "01");
        await AddAsync(database, 2, at, "fusion-a", "共享工作台", "01", "01");
        await AddAsync(database, 3, at, "fusion-A", "共享工作台", "01", "01");
        var request = new ParcelAnalysisRequest { View = "chutes", FromDate = at.Date, ToDate = at.Date };
        var report = await Reader(database).ReadAsync(request, default);
        Assert.Equal(3, report.ParcelCount);
        Assert.Equal(2, report.ChuteRoutes.Count);
        Assert.Equal(2, report.ActualChuteHeatmap!.Cells.Count);
        Assert.Equal(2, report.TargetChuteHeatmap!.Cells.Count);
        Assert.Equal(2, Assert.Single(report.ChuteRoutes, route => route.SourceInstanceId == "fusion-a").Count);
        Assert.Equal(1, Assert.Single(report.ActualChuteHeatmap.Cells, cell => cell.SourceInstanceId == "fusion-A").Count);
        foreach (var source in new[] { "fusion-a", "fusion-A" }) {
            var scoped = await Reader(database).ReadAsync(request with { SourceInstanceId = source }, default);
            Assert.Equal(source == "fusion-a" ? 2 : 1, scoped.ParcelCount);
            Assert.All(scoped.Items, parcel => Assert.Equal(source, parcel.SourceInstanceId));
            Assert.Equal(source, Assert.Single(scoped.ChuteRoutes).SourceInstanceId);
            Assert.Equal(source, Assert.Single(scoped.ActualChuteHeatmap!.Cells).SourceInstanceId);
            Assert.Equal(source, Assert.Single(scoped.TargetChuteHeatmap!.Cells).SourceInstanceId);
        }
    }

    /// <summary>真实提供器离线翻译验证来源筛选与两种格口聚合，不以SQLite默认规则冒充生产排序规则。</summary>
    [Theory]
    [InlineData("MySql", "utf8mb4_bin")]
    [InlineData("SqlServer", "Latin1_General_100_BIN2")]
    [InlineData("Oracle", null)]
    [InlineData("SQLite", null)]
    public void SourceScopeAndChuteGroupsTranslateWithMatchingIdentityCollation(string provider, string? expectedCollation) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ParcelAnalysisSnapshot>.Create<Parcel>(db, ["202610", ""]);
        var from = new DateTime(2026, 10, 1);
        var cohort = read.Query(["202610", ""], nameof(Parcel.CreatedTime), from, from.AddDays(1), false);
        var collation = ParcelAnalysisReadService.SourceCollation(db.Database.ProviderName);
        Assert.Equal(expectedCollation, collation);
        var scoped = ParcelAnalysisReadService.FilterSource(cohort, "fusion-a", collation);
        var queries = new[] { scoped.ToQueryString(), ParcelAnalysisReadService.ChuteRoutes(scoped, collation).ToQueryString(),
            ParcelChuteHeatmapReader.Cells(scoped, true, collation).ToQueryString(), ParcelChuteHeatmapReader.Cells(scoped, false, collation).ToQueryString() };
        foreach (var sql in queries) {
            Assert.Contains(nameof(Parcel.SourceInstanceId), sql, StringComparison.Ordinal);
            if (expectedCollation is not null) Assert.Contains("COLLATE " + expectedCollation, sql, StringComparison.Ordinal);
            else Assert.DoesNotContain("COLLATE", sql, StringComparison.OrdinalIgnoreCase);
        }
        // 聚合查询也单独检查未筛选总体，避免仅因WHERE有COLLATE而掩盖GROUP BY遗漏。
        foreach (var sql in new[] { ParcelAnalysisReadService.ChuteRoutes(cohort, collation).ToQueryString(),
            ParcelChuteHeatmapReader.Cells(cohort, true, collation).ToQueryString(), ParcelChuteHeatmapReader.Cells(cohort, false, collation).ToQueryString() }) {
            Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
            if (expectedCollation is not null) Assert.Contains("COLLATE " + expectedCollation, sql, StringComparison.Ordinal);
            else Assert.DoesNotContain("COLLATE", sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>空范围没有虚构格口，也不将缺失信息伪造为零票格口。</summary>
    [Fact]
    public async Task EmptyHeatmapHasNoInventedChutes() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var report = await Reader(database).ReadAsync(new ParcelAnalysisRequest {
            View = "chutes", FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 1)
        }, default);
        Assert.Empty(report.ActualChuteHeatmap!.Cells);
        Assert.Empty(report.TargetChuteHeatmap!.Cells);
        Assert.Equal(0, report.ActualChuteHeatmap.SampleCount);
        Assert.Equal(0, report.TargetChuteHeatmap.MissingCodeCount);
        Assert.False(report.ActualChuteHeatmap.Truncated);
    }

    /// <summary>沿用生产报表预算，不读取包裹明细进行内存聚合。</summary>
    private static ParcelAnalysisReadService Reader(RelationalParcelTestDatabase database, int maxRows = 100000) => new(
        database.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions { MaxReportRows = maxRows })), database.Partitions);

    /// <summary>写入来源事实，保留可比、缺失及兜底包裹的真实投影。</summary>
    private static async Task AddAsync(RelationalParcelTestDatabase database, long id, DateTime at, string source,
        string workstation, string? target, string? actual, bool fallback = false) {
        var fact = new ParcelProcessingRecord {
            RecordId = "detect-" + id, PayloadHash = "detect-" + id, SourceInstanceId = source, SourceRunId = "run-1", SourceParcelId = id,
            WorkstationName = workstation, Barcode = "PKG-" + id, Stage = ParcelProcessingStage.Detected,
            OccurredAt = at, RecordedAt = at, PartitionTime = at
        };
        Assert.True((await database.Processing.AppendAsync(fact, default)).IsSuccess);
        if (target != null) Assert.True((await database.Processing.AppendAsync(fact with {
            RecordId = "target-" + id, PayloadHash = "target-" + id, OccurredAt = at.AddSeconds(1),
            Stage = ParcelProcessingStage.ChuteAssigned, IsSuccess = true, TargetChuteCode = target, IsFallback = fallback
        }, default)).IsSuccess);
        if (actual != null) Assert.True((await database.Processing.AppendAsync(fact with {
            RecordId = "actual-" + id, PayloadHash = "actual-" + id, OccurredAt = at.AddSeconds(2),
            Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = actual
        }, default)).IsSuccess);
    }
}
