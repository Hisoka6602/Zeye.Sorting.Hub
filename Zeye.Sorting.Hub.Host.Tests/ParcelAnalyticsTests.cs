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
    /// <summary>跨日汇总、NoRead、异常、格口差异与加权完成耗时均来自已保存的事实。</summary>
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
        Assert.Equal(first.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Assert.Single(report.DailySorting).Date);
        Assert.Equal(1, Assert.Single(report.DailySorting).SortedCount);
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
        Assert.Null(eventDay.MinimumCreationIntervalMilliseconds);
        Assert.Null(eventDay.ActualSortingThroughputPerHour);
        Assert.Null(eventDay.TheoreticalSortingThroughputPerHour);
        Assert.Equal(0, eventDay.CreationIntervalSampleCount);
        Assert.Equal(1, eventDay.ProcessingEventCount);
        Assert.Equal(1, eventDay.FailedAttemptCount);
    }

    /// <summary>分拣趋势使用完成日，更早入库的包裹不会被误算为完成日入库。</summary>
    [Fact]
    public async Task DailySortingUsesCompletionDateRatherThanIntakeDate() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var detectedAt = new DateTime(2026, 9, 28, 10, 0, 0);
        var completedAt = detectedAt.AddDays(2).AddSeconds(5);
        await SaveAsync(database, Fact("detected-cross-day", 42, detectedAt));
        await SaveAsync(database, Fact("sorted-cross-day", 42, completedAt) with {
            Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = "X01"
        });

        var report = await Reader(database).GetAsync(completedAt.Date, completedAt.Date, default);
        Assert.Equal(0, report.DetectedCount);
        Assert.Empty(report.Daily);
        var sortingDay = Assert.Single(report.DailySorting);
        Assert.Equal("2026-09-30", sortingDay.Date);
        Assert.Equal(1, sortingDay.SortedCount);
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
        var query = await ParcelPartitionQueryBuilder.BuildParcelsByCreatedTimeAsync(db, database.Partitions, first.Date, first.Date.AddDays(1), default);
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
        Assert.Null(empty.MedianCreationIntervalMilliseconds);
        Assert.Null(empty.MinimumCreationIntervalMilliseconds);
        Assert.Null(empty.ActualSortingThroughputPerHour);
        Assert.Null(empty.TheoreticalSortingThroughputPerHour);
        Assert.Equal(0, empty.CreationIntervalSampleCount);
        Assert.Empty(empty.Workstations);
        Assert.Empty(empty.DailySorting);
        Assert.Equal(0, empty.ProcessingEventCount);
    }

    /// <summary>创建间隔采用中位数抵抗长暂停；未完成的成功入库包裹也参与产能估算。</summary>
    [Fact]
    public async Task SortingThroughputUsesMedianCreationGapInsteadOfCompletionDuration() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        foreach (var offset in new[] { 0, 500, 1000, 61000 })
            await SaveAsync(database, Fact("cadence-" + offset, offset + 1, start.AddMilliseconds(offset)));

        var report = await Reader(database, maxRows: 1).GetAsync(start.Date, start.Date, default);
        Assert.Equal(4, report.DetectedCount);
        Assert.Equal(0, report.CompletedCount);
        Assert.Null(report.AverageLifecycleSeconds);
        Assert.Equal(3, report.CreationIntervalSampleCount);
        Assert.Equal(500m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(500m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(7200m, report.ActualSortingThroughputPerHour);
        Assert.Equal(7200m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>偶数间隔取中间两项平均，保留半毫秒；来源事件耗时和到达顺序不能替代创建时间。</summary>
    [Fact]
    public async Task EvenMedianPreservesFractionalMillisecondsAndOrdersCreationTimes() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        foreach (var offset in new[] { 201, 0, 100 })
            await SaveAsync(database, Fact("even-" + offset, offset + 1, start.AddSeconds(offset)) with {
                RecordedAt = start.AddMilliseconds(offset)
            });

        var report = await Reader(database).GetAsync(start.Date, start.Date, default);
        Assert.Equal(2, report.CreationIntervalSampleCount);
        Assert.Equal(100.5m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(100m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(3_600_000m / 100.5m, report.ActualSortingThroughputPerHour);
        Assert.Equal(36000m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>最短间隔可以来自时间窗口首部或尾部，不能只对中位数选中的间隔取最小值。</summary>
    [Theory]
    [InlineData(100, 1000, 2000)]
    [InlineData(2000, 1000, 100)]
    public async Task TheoreticalThroughputUsesShortestGapAcrossEntireWindow(int firstGap, int secondGap, int thirdGap) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        foreach (var offset in new[] { firstGap + secondGap + thirdGap, firstGap, 0, firstGap + secondGap })
            await SaveAsync(database, Fact("shortest-" + offset, offset + 1, start.AddMilliseconds(offset)));

        var report = await Reader(database, maxRows: 1).GetAsync(start.Date, start.Date, default);
        Assert.Equal(4, report.DetectedCount);
        Assert.Equal(3, report.CreationIntervalSampleCount);
        Assert.Equal(1000m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(100m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(3600m, report.ActualSortingThroughputPerHour);
        Assert.Equal(36000m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>跨日分表和基础表必须先合并再求间隔，重试、未检测消息及范围外创建均不加样本。</summary>
    [Fact]
    public async Task CreationIntervalsMergePartitionsAndExcludeDuplicatesAndOutOfRangeRows() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 23, 59, 59, 900);
        var baseFact = Fact("legacy-creation", 99, start) with { ParcelId = 9999 };
        var legacy = Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Parcel.CreateDetected(9999, baseFact, start);
        legacy.ApplyProcessingRecords([baseFact]);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(legacy);
            await db.SaveChangesAsync();
        }
        foreach (var offset in new[] { 75, 125, 700 })
            await SaveAsync(database, Fact("cross-" + offset, offset + 1, start.AddMilliseconds(offset)));
        var duplicate = await database.Processing.AppendAsync(Fact("cross-75", 76, start.AddMilliseconds(75)), default);
        Assert.True(duplicate.IsSuccess);
        Assert.True(duplicate.Value!.IsDuplicate);
        await SaveAsync(database, Fact("failed-attempt", 76, start.AddMilliseconds(100)) with {
            Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false
        });
        await SaveAsync(database, Fact("not-detected", 88, start.AddMilliseconds(90)) with {
            Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false
        });
        await SaveAsync(database, Fact("unbound-creation", null, start.AddMilliseconds(110)) with {
            Stage = ParcelProcessingStage.DwsReceived
        });
        await SaveAsync(database, Fact("before-window", 77, start.Date.AddTicks(-1)));
        await SaveAsync(database, Fact("after-window", 78, start.Date.AddDays(2)));

        var report = await Reader(database).GetAsync(start.Date, start.Date.AddDays(1), default);
        Assert.Equal(4, report.DetectedCount);
        Assert.Equal(3, report.CreationIntervalSampleCount);
        Assert.Equal(75m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(50m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(48000m, report.ActualSortingThroughputPerHour);
        Assert.Equal(72000m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>没有包裹、单票或同毫秒创建都无法估算有效小时产能。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task InsufficientOrZeroIntervalsDoNotInventAThroughput(int count) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        for (var index = 0; index < count; index++)
            await SaveAsync(database, Fact("same-time-" + index, index + 1, start));

        var report = await Reader(database).GetAsync(start.Date, start.Date, default);
        Assert.Equal(count, report.DetectedCount);
        Assert.Null(report.MedianCreationIntervalMilliseconds);
        Assert.Null(report.MinimumCreationIntervalMilliseconds);
        Assert.Null(report.ActualSortingThroughputPerHour);
        Assert.Null(report.TheoreticalSortingThroughputPerHour);
        Assert.Equal(0, report.CreationIntervalSampleCount);
    }

    /// <summary>同时间创建的零间隔不参与分母，随后正常节拍仍能估算。</summary>
    [Fact]
    public async Task ZeroIntervalsAreIgnoredWithoutDiscardingPositiveIntervals() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        await SaveAsync(database, Fact("zero-a", 1, start));
        await SaveAsync(database, Fact("zero-b", 2, start));
        await SaveAsync(database, Fact("positive-c", 3, start.AddMilliseconds(500)));

        var report = await Reader(database).GetAsync(start.Date, start.Date, default);
        Assert.Equal(1, report.CreationIntervalSampleCount);
        Assert.Equal(500m, report.MedianCreationIntervalMilliseconds);
        Assert.Equal(500m, report.MinimumCreationIntervalMilliseconds);
        Assert.Equal(7200m, report.ActualSortingThroughputPerHour);
        Assert.Equal(7200m, report.TheoreticalSortingThroughputPerHour);
    }

    /// <summary>构造报表服务，复用仓库现有查询预算规划器。</summary>
    private static ParcelAnalyticsReadService Reader(RelationalParcelTestDatabase database, int maxDays = 31, int maxRows = 100000) => new(
        database.Factory,
        new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions { MaxReportTimeRangeDays = maxDays, MaxReportRows = maxRows })), database.Partitions);

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
