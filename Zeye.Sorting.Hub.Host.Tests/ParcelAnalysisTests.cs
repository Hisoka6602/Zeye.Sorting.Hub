using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>以真实SQLite分表验证分析总体、分位数、来源隔离和HTTP校验。</summary>
public sealed class ParcelAnalysisTests {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly int[] Cached010000Values = new[] { 0, 10000 };

    /// <summary>异常下钻、NoRead及路由阻断共享完整入库总体，不把当前页当作统计总体。</summary>
    [Fact]
    public async Task ExceptionsUseIntakeCohortAndSourceFiltersAcrossPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var start = new DateTime(2026, 10, 1, 10, 0, 0);
        await SaveAsync(database, Fact("detect-1", 1, start));
        await SaveAsync(database, Fact("exception-1", 1, start.AddSeconds(1)) with {
            Stage = ParcelProcessingStage.ParcelException, ExceptionCode = "ParcelSpacingViolation"
        });
        await SaveAsync(database, Fact("detect-2", 2, start.AddDays(1)) with { Barcode = "NoRead" });
        await SaveAsync(database, Fact("blocked-2", 2, start.AddDays(1).AddSeconds(1)) with {
            Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false, IsRoutingBlocked = true
        });
        await SaveAsync(database, Fact("detect-other", 3, start.AddDays(1), "other-source"));
        await SaveAsync(database, Fact("old-scan-new-intake", 4, start.AddDays(-2)) with {
            RecordedAt = start.AddDays(1), PartitionTime = start.AddDays(1)
        });
        var request = Request(start, "exceptions") with { ToDate = start.Date.AddDays(1), SourceInstanceId = "analysis-a" };
        var report = await Reader(database).ReadAsync(request, default);
        Assert.Equal(3, report.ParcelCount);
        Assert.Equal(1, report.ExceptionCount);
        Assert.Equal(1, report.NoReadCount);
        Assert.Equal(1, report.RoutingBlockedCount);
        Assert.Equal(1, report.FilteredCount);
        Assert.Equal("包裹间距违规", Assert.Single(report.ExceptionTypes).Name);
        Assert.Equal("包裹间距违规", Assert.Single(report.Items).ExceptionName);
        var noread = await Reader(database).ReadAsync(request with { Issue = "noread" }, default);
        Assert.Equal(3, noread.ParcelCount);
        Assert.Equal("NoRead", Assert.Single(noread.Items).BarCodes);
        var blocked = await Reader(database).ReadAsync(request with { Issue = "blocked" }, default);
        Assert.True(Assert.Single(blocked.Items).IsRoutingBlocked);
    }

    /// <summary>分位数基于全部有效样本，偶数中位数保留精度，区间采用左闭右开且不补造缺失耗时。</summary>
    [Fact]
    public async Task DurationQuantilesAndBucketsUseAllSamplesBeyondFirstPage() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 10, 1, 10, 0, 0);
        var values = Cached010000Values.Concat(Enumerable.Range(1, 20).Select(index => index * 100)).ToArray();
        for (var index = 0; index < values.Length; index++) {
            var at = start.AddSeconds(index * 20);
            await SaveAsync(database, Fact("0-detect-" + index, index + 1, at));
            await SaveAsync(database, Fact("1-complete-" + index, index + 1, at.AddMilliseconds(values[index])) with {
                Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = "X01"
            });
        }
        await SaveAsync(database, Fact("pending", 50, start.AddHours(1)));
        await SaveAsync(database, Fact("bad-detect", 51, start.AddHours(2)));
        await SaveAsync(database, Fact("bad-complete", 51, start.AddHours(2).AddMilliseconds(-1)) with {
            Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = "X01"
        });
        var request = Request(start, "duration");
        var report = await Reader(database).ReadAsync(request, default);
        Assert.Equal(24, report.ParcelCount);
        Assert.Equal(23, report.CompletedCount);
        Assert.Equal(22, report.LifecycleSampleCount);
        Assert.Equal(31000m / 22, report.AverageMilliseconds);
        Assert.Equal(1050m, report.MedianMilliseconds);
        Assert.Equal(2000, report.P95Milliseconds);
        Assert.Equal(0, report.MinimumMilliseconds);
        Assert.Equal(10000, report.MaximumMilliseconds);
        Assert.Equal([5L, 5L, 10L, 1L, 1L], report.DurationBuckets.Select(bucket => bucket.Count));
        Assert.Equal(20, report.Items.Count);
        Assert.Equal(22, report.FilteredCount);
        Assert.Equal(10000, report.Items[0].LifecycleMilliseconds);
        var second = await Reader(database).ReadAsync(request with { PageNumber = 2 }, default);
        Assert.Equal(2, second.Items.Count);
        var bucketReport = await Reader(database).ReadAsync(request with { MinimumMilliseconds = 500, MaximumMilliseconds = 1000 }, default);
        Assert.Equal(5, bucketReport.FilteredCount);
        Assert.Equal(1050m, bucketReport.MedianMilliseconds);
        Assert.All(bucketReport.Items, parcel => Assert.InRange(parcel.LifecycleMilliseconds!.Value, 500, 999));
    }

    /// <summary>不同来源的同名格口分别分组，缺失格口不误算差异，截断分布不能改变总体。</summary>
    [Fact]
    public async Task ChuteRoutesPreserveSourcesAndUseOnlyComparablePairs() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 10, 1, 10, 0, 0);
        foreach (var entry in new[] { (Id: 1L, Source: "analysis-a", Target: " x01 ", Actual: "X01"),
            (Id: 2L, Source: "analysis-a", Target: "X01", Actual: "X02"), (Id: 3L, Source: "other-source", Target: "X01", Actual: "X02") }) {
            await SaveAsync(database, Fact("detect-" + entry.Id, entry.Id, start, entry.Source));
            await SaveAsync(database, Fact("target-" + entry.Id, entry.Id, start.AddSeconds(1), entry.Source) with {
                Stage = ParcelProcessingStage.ChuteAssigned, IsSuccess = true, TargetChuteCode = entry.Target, IsFallback = entry.Id == 2
            });
            await SaveAsync(database, Fact("landed-" + entry.Id, entry.Id, start.AddSeconds(2), entry.Source) with {
                Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = entry.Actual
            });
        }
        await SaveAsync(database, Fact("missing-codes", 4, start));
        var request = Request(start, "chutes");
        var report = await Reader(database).ReadAsync(request, default);
        Assert.Equal(4, report.ParcelCount);
        Assert.Equal(3, report.ComparableChuteCount);
        Assert.Equal(2, report.ChuteMismatchCount);
        Assert.Equal(1, report.FallbackCount);
        Assert.Equal(4, report.ChuteRoutes.Count);
        var mismatches = await Reader(database).ReadAsync(request with { MismatchOnly = true }, default);
        Assert.Equal(2, mismatches.FilteredCount);
        Assert.Equal(4, mismatches.ParcelCount);
        var route = await Reader(database).ReadAsync(request with {
            SourceInstanceId = "other-source", TargetChuteCode = "X01", ActualChuteCode = "X02"
        }, default);
        Assert.Equal("other-source", Assert.Single(route.Items).SourceInstanceId);
        var rawCode = await Reader(database).ReadAsync(request with {
            SourceInstanceId = "analysis-a", TargetChuteCode = " x01 ", ActualChuteCode = "X01"
        }, default);
        Assert.Equal(" x01 ", Assert.Single(rawCode.Items).TargetChuteCode);
        var limited = await Reader(database, maxRows: 1).ReadAsync(request, default);
        Assert.True(limited.ChuteRoutesTruncated);
        Assert.Single(limited.ChuteRoutes);
        Assert.Equal(4, limited.ParcelCount);
        Assert.Equal(4, limited.FilteredCount);
    }

    /// <summary>接口日期及过滤参数有显式错误；空总体保留未知均值和分位数。</summary>
    [Fact]
    public async Task HttpValidationAndEmptyDurationDoNotInventValues() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton(database.Partitions);
        builder.Services.AddSingleton(new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())));
        builder.Services.AddScoped<IParcelAnalysisReadService, ParcelAnalysisReadService>();
        await using var app = builder.Build();
        app.MapParcelAnalysisApis();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/api/parcels/analysis?fromDate=2026-10-01&toDate=2026-10-01";
        foreach (var invalid in new[] { "/api/parcels/analysis", path + "&view=unknown", path + "&pageNumber=0", path + "&view=duration&durationType=unknown",
            path + "&minimumMilliseconds=1000&maximumMilliseconds=500", path + "&exceptionType=999",
            "/api/parcels/analysis?fromDate=2026-10-01Z&toDate=2026-10-01", "/api/parcels/analysis?fromDate=2026-10-01&toDate=2026-11-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(invalid)).StatusCode);
        var response = await client.GetFromJsonAsync<ParcelAnalysisResponse>(path + "&view=duration");
        Assert.NotNull(response);
        Assert.Equal(0, response.ParcelCount);
        Assert.Null(response.AverageMilliseconds);
        Assert.Null(response.MedianMilliseconds);
        Assert.Null(response.P95Milliseconds);
        Assert.Empty(response.Items);
        Assert.All(response.DurationBuckets, bucket => Assert.Equal(0, bucket.Count));
        var dws = await client.GetFromJsonAsync<ParcelAnalysisResponse>(path + "&view=duration&durationType=dws");
        Assert.NotNull(dws!.DurationAnalysis);
        Assert.Equal("dws", dws.DurationType);
        Assert.Null(dws.DurationAnalysis.MedianMilliseconds);
        Assert.Null(dws.DurationAnalysis.P95Milliseconds);
        Assert.Empty(dws.DurationAnalysis.Items);
    }

    /// <summary>使用生产同款报表预算。</summary>
    private static ParcelAnalysisReadService Reader(RelationalParcelTestDatabase database, int maxRows = 100000) => new(
        database.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions { MaxReportRows = maxRows })), database.Partitions);

    /// <summary>生成单日分析请求。</summary>
    private static ParcelAnalysisRequest Request(DateTime at, string view) => new() { View = view, FromDate = at.Date, ToDate = at.Date };

    /// <summary>生成明确来源、工作台和本地时间的事实。</summary>
    private static ParcelProcessingRecord Fact(string recordId, long sourceId, DateTime at, string source = "analysis-a") => new() {
        RecordId = recordId, SourceInstanceId = source, SourceRunId = "run-1", SourceParcelId = sourceId,
        Stage = ParcelProcessingStage.Detected, OccurredAt = at, RecordedAt = at, PartitionTime = at,
        Barcode = "PKG-" + sourceId, WorkstationName = "工作台-" + source, PayloadHash = recordId
    };

    /// <summary>写入真实来源仓储并断言落库成功。</summary>
    private static async Task SaveAsync(RelationalParcelTestDatabase database, ParcelProcessingRecord record) {
        var result = await database.Processing.AppendAsync(record, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
