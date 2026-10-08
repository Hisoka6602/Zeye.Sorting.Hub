using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
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
