using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>用真实 SQLite 物理分表验证来源包裹和处理事实的不同统计总体。</summary>
public sealed class ParcelAnalyticsTests {
    /// <summary>跨日汇总、NoRead、异常、格口差异与加权时效均来自已保存的事实。</summary>
    [Fact]
    public async Task ReportAggregatesPhysicalPartitionsAndPreservesDistinctDenominators() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var first = new DateTime(2026, 9, 28, 10, 0, 0);
        var second = first.AddDays(1);
        await SaveAsync(database, Fact("detected-a", 1, first));
        await SaveAsync(database, Fact("bound-a", 1, first.AddSeconds(1)) with {
            Stage = ParcelProcessingStage.DwsBound, IsSuccess = true, FinalSourceParcelId = 1,
            Barcode = "PKG-A", WeightGrams = 1234.567m, WidthMm = null,
            VolumeMm3 = null, VolumetricWeightGrams = 2200m
        });
        await SaveAsync(database, Fact("failed-a", 1, first.AddSeconds(2)) with { Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false });
        await SaveAsync(database, Fact("target-a", 1, first.AddSeconds(3)) with { Stage = ParcelProcessingStage.ChuteAssigned, IsSuccess = true, TargetChuteCode = "X01" });
        await SaveAsync(database, Fact("landed-a", 1, first.AddSeconds(10)) with { Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = "X02" });
        await SaveAsync(database, Fact("detected-b", 2, second) with { Barcode = "NoRead", WorkstationName = "W-B" });
        await SaveAsync(database, Fact("exception-b", 2, second.AddSeconds(1)) with { Stage = ParcelProcessingStage.ParcelException, ExceptionCode = "ParcelSpacingViolation" });
        await SaveAsync(database, Fact("unbound", null, second.AddSeconds(2)) with { Stage = ParcelProcessingStage.DwsReceived, RawPayload = "raw", DecisionReason = "ambiguous" });

        var report = await Reader(database).GetAsync(first.Date, second.Date, default);
        Assert.Equal(2, report.DetectedCount);
        Assert.Equal(1, report.CompletedCount);
        Assert.Equal(1, report.ExceptionCount);
        Assert.Equal(1, report.NoReadCount);
        Assert.Equal(1, report.ChuteMismatchCount);
        Assert.Equal(10m, report.AverageLifecycleSeconds);
        Assert.Equal(8, report.ProcessingEventCount);
        Assert.Equal(1, report.FailedAttemptCount);
        Assert.Equal(1, report.UnboundDwsEventCount);
        Assert.Equal(2, report.Daily.Count);
        Assert.Equal("包裹间距违规", Assert.Single(report.ExceptionTypes).Name);
        Assert.Contains(report.Workstations, x => x.Name == "W-B" && x.Count == 1);
    }

    /// <summary>晚到事实按发生日期查询，不能被包裹首次入库日期吞并或作为新的包裹。</summary>
    [Fact]
    public async Task LateEventHasOwnEventDateWithoutChangingParcelCohort() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detectedAt = new DateTime(2026, 9, 28, 10, 0, 0);
        await SaveAsync(database, Fact("detected", 7, detectedAt));
        await SaveAsync(database, Fact("late-upload", 7, detectedAt.AddDays(2)) with { Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false });
        var eventDay = await Reader(database).GetAsync(detectedAt.Date.AddDays(2), detectedAt.Date.AddDays(2), default);
        Assert.Equal(0, eventDay.DetectedCount);
        Assert.Empty(eventDay.Daily);
        Assert.Null(eventDay.AverageLifecycleSeconds);
        Assert.Equal(1, eventDay.ProcessingEventCount);
        Assert.Equal(1, eventDay.FailedAttemptCount);
    }

    /// <summary>快照日期窗口只合并重叠的入库周期，仍保留基础表兼容历史数据。</summary>
    [Fact]
    public async Task ParcelCohortPrunesNonOverlappingPhysicalPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var first = new DateTime(2026, 9, 28, 10, 0, 0);
        var second = first.AddDays(1);
        await SaveAsync(database, Fact("prune-a", 100, first));
        await SaveAsync(database, Fact("prune-b", 101, second));
        await using var db = await database.Factory.CreateDbContextAsync();
        var query = await ParcelPartitionQueryBuilder.BuildParcelsByCreatedTimeAsync(db, first.Date, first.Date.AddDays(1), default);
        var sql = query.ToQueryString();
        Assert.Contains("Parcels_20260928", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Parcels_20260929", sql, StringComparison.Ordinal);
        Assert.Contains("Parcels", sql, StringComparison.Ordinal);
        var report = await Reader(database).GetAsync(first.Date, first.Date, default);
        Assert.Equal(1, report.DetectedCount);
    }

    /// <summary>严格限制本地日期窗口，空数据不制造零秒平均值。</summary>
    [Fact]
    public async Task DateBudgetAndEmptyStateAreExplicit() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var reader = Reader(database, maxDays: 2);
        var date = new DateTime(2026, 9, 28);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetAsync(date, date.AddDays(2), default));
        var empty = await reader.GetAsync(date, date, default);
        Assert.Equal(0, empty.DetectedCount);
        Assert.Null(empty.AverageLifecycleSeconds);
        Assert.Empty(empty.Workstations);
        Assert.Equal(0, empty.ProcessingEventCount);
    }

    /// <summary>构造报表服务，复用仓库现有查询预算规划器。</summary>
    private static ParcelAnalyticsReadService Reader(RelationalParcelTestDatabase database, int maxDays = 31) => new(
        database.Factory,
        database.Partitions,
        new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions { MaxReportTimeRangeDays = maxDays })));

    /// <summary>构造稳定来源身份的本地处理事实。</summary>
    private static ParcelProcessingRecord Fact(string recordId, long? sourceParcelId, DateTime time) => new() {
        RecordId = recordId, SourceInstanceId = "analytics-test", SourceRunId = "run-1", SourceParcelId = sourceParcelId,
        Stage = ParcelProcessingStage.Detected, OccurredAt = time, RecordedAt = time, PartitionTime = time, PayloadHash = recordId
    };

    /// <summary>断言事实写入成功后继续检查数据库聚合。</summary>
    private static async Task SaveAsync(RelationalParcelTestDatabase database, ParcelProcessingRecord record) {
        var result = await database.Processing.AppendAsync(record, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
