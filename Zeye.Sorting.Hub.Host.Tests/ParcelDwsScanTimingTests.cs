using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>真实关系分表验证扫码端点、精度、来源隔离、缺失数据与独立趋势。</summary>
public sealed class ParcelDwsScanTimingTests {
    /// <summary>接收时间区别于设备时间、晚到绑定和Hub入库时间，跨来源及运行会话不串票。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScanUsesSourceReceptionAndSupportsThresholdsAndSourceComparison(bool useIndex) {
        await using var db = new RelationalParcelTestDatabase("PerDay"); await db.InitializeAsync();
        using var cache = new ParcelDwsConsistencyCache(); var reader = Reader(db, cache);
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Detect(db, "a", "run-1", 1, at);
        var first = Measurement("rx-a1", "message-1", "a", "run-1", null, at) with {
            ReceivedAt = at.AddTicks(1234567), MeasuredAt = at.AddSeconds(-5), OccurredAt = at.AddHours(2), RecordedAt = at.AddHours(3)
        };
        await Save(db, first);
        await Save(db, first with { RecordId = "binding-a1", PayloadHash = "binding-a1", Stage = ParcelProcessingStage.DwsBound,
            SourceParcelId = 1, FinalSourceParcelId = 1, IsSuccess = true, OccurredAt = at.AddDays(1) });
        await Detect(db, "a", "run-1", 2, at.AddMinutes(1));
        await Save(db, Measurement("rx-a2", "message-2", "a", "run-1", 2, at.AddMinutes(1).AddTicks(5001000)));
        await Detect(db, "b", "run-1", 1, at.AddMinutes(2));
        await Save(db, Measurement("rx-b1", "message-1", "b", "run-1", 1, at.AddMinutes(2).AddSeconds(1)));
        await Detect(db, "a", "run-2", 1, at.AddMinutes(3));
        await Save(db, Measurement("rx-run2", "message-1", "a", "run-2", 1, at.AddMinutes(3).AddMilliseconds(500)));
        if (useIndex) {
            var backfill = new ParcelDwsMeasurementBackfillService(db.Factory, db.Partitions);
            while (await backfill.RunBatchAsync(default) > 0) { }
        }
        var request = Request(at) with { DetailBarcode = "SCAN", SortBy = "scan-duration-p95", OnlyDeviations = true };
        var report = await reader.ReadAsync(request, default);
        Assert.Equal(4, report.MeasurementCount); Assert.Equal(4, report.ScanTimingSampleCount); Assert.Equal(0, report.MissingScanTimingCount);
        Assert.Equal(1, report.DuplicateRecordCount); Assert.Equal(1, report.DeviationBarcodeCount);
        var group = Assert.Single(report.Items); Assert.True(group.ScanDurationDeviates); Assert.False(group.WeightDeviates); Assert.False(group.VolumeDeviates);
        Assert.Equal(500.05m, group.ScanDuration.Median); Assert.Equal(530.889175m, group.ScanDuration.Average);
        Assert.Equal(1000m, group.ScanDuration.P95); Assert.Equal(876.5433m, group.ScanDuration.Spread);
        var sample = Assert.Single(report.Detail!.Items, item => item.RecordId == "rx-a1");
        Assert.Equal(123.4567m, sample.ScanDurationMilliseconds); Assert.Equal(at, sample.ScanStartedAt);
        Assert.Equal(first.ReceivedAt, sample.ScanCompletedAt); Assert.Equal(first.MeasuredAt, sample.MeasuredAt);
        Assert.Equal("source-received", sample.ScanTimingBasis); Assert.Null(sample.ScanTimingUnavailableReason);
        Assert.Equal(4, report.Detail.ScanTrend.Count); Assert.Equal(4, report.Detail.Items.Select(item => item.ParcelId).Distinct().Count());
        Assert.All(report.Sources, source => Assert.NotNull(source.MedianScanDurationDeviationPercent));
        var relaxed = await reader.ReadAsync(request with { ScanDurationToleranceMilliseconds = 1000m, SortBy = "scan-duration" }, default);
        Assert.Empty(relaxed.Items); Assert.Equal(report.GeneratedAt, relaxed.GeneratedAt); Assert.Equal(4, relaxed.ScanTimingSampleCount);
    }

    /// <summary>缺失、倒序、不可靠和冲突的扫码时间保持未知，不污染重量、体积和测量身份统计。</summary>
    [Fact]
    public async Task MissingAndConflictingScanTimesKeepOtherMeasurementsUsable() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Detect(db, "a", "run-1", 1, at, reliable: false);
        await Save(db, Measurement("unreliable", "m-1", "a", "run-1", 1, at.AddMilliseconds(200)));
        await Detect(db, "a", "run-1", 2, at);
        await Save(db, Measurement("reversed", "m-2", "a", "run-1", 2, at.AddMilliseconds(-1)));
        await Detect(db, "a", "run-1", 3, at);
        await Save(db, Measurement("only-binding", "m-3", "a", "run-1", 3, at.AddMilliseconds(200)) with {
            Stage = ParcelProcessingStage.DwsBound, IsSuccess = true, FinalSourceParcelId = 3 });
        await Save(db, Measurement("unbound", "m-4", "a", "run-1", null, at.AddMilliseconds(200)));
        await Detect(db, "other", "run-1", 5, at);
        await Save(db, Measurement("missing-detection", "m-5", "a", "run-1", 5, at.AddMilliseconds(200)));
        await Detect(db, "a", "run-1", 6, at);
        await Save(db, Measurement("wrong-run", "m-6", "a", "run-2", 6, at.AddMilliseconds(200)));
        await Detect(db, "a", "run-1", 7, at);
        // 写仓储会拒绝同一来源包裹的二次检测；直接种入旧库冲突事实，验证只读分析也不会伪造耗时。
        await using (var legacy = await db.Partitions.CreateContextAsync(db.Partitions.Resolve(at).Suffix, default)) {
            var detection = await legacy.Set<ParcelProcessingRecord>().SingleAsync(row => row.SourceParcelId == 7 && row.Stage == ParcelProcessingStage.Detected);
            legacy.Add(detection with { Key = "legacy-conflicting-detection", RecordId = "detect-conflict", OccurredAt = at.AddMilliseconds(1) });
            await legacy.SaveChangesAsync();
        }
        await Save(db, Measurement("conflicting-start", "m-7", "a", "run-1", 7, at.AddMilliseconds(200)));
        await Detect(db, "a", "run-1", 8, at);
        var collision = Measurement("conflicting-end", "m-8", "a", "run-1", 8, at.AddMilliseconds(200));
        await Save(db, collision);
        await Save(db, collision with { RecordId = "conflicting-end-copy", PayloadHash = "conflicting-end-copy", ReceivedAt = at.AddMilliseconds(201) });
        await Detect(db, "a", "run-1", 9, at);
        var unknownBarcode = Measurement("no-received-barcode", "m-9", "a", "run-1", null, at.AddMilliseconds(200));
        await Save(db, unknownBarcode with { Barcode = null });
        await Save(db, unknownBarcode with { RecordId = "bound-barcode", PayloadHash = "bound-barcode", Stage = ParcelProcessingStage.DwsBound,
            SourceParcelId = 9, FinalSourceParcelId = 9, IsSuccess = true });
        var report = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SCAN" }, default);
        Assert.Equal(9, report.MeasurementCount); Assert.Equal(0, report.ScanTimingSampleCount); Assert.Equal(9, report.MissingScanTimingCount);
        Assert.Equal(0, report.ConflictingMeasurementCount); Assert.Empty(report.Detail!.ScanTrend);
        Assert.Equal(9, report.Detail.Summary.Weight.Count); Assert.Equal(9, report.Detail.Summary.Volume.Count);
        Assert.Null(report.Detail.Summary.ScanDuration.Median); Assert.Null(report.Detail.Summary.ScanDuration.Average); Assert.Null(report.Detail.Summary.ScanDuration.P95);
        var reasons = report.Detail.Items.ToDictionary(sample => sample.RecordId, sample => sample.ScanTimingUnavailableReason);
        Assert.Equal("unreliable-detection-time", reasons["unreliable"]); Assert.Equal("reversed-scan-time", reasons["reversed"]);
        Assert.Equal("missing-scan-result", reasons["only-binding"]); Assert.Equal("missing-parcel-identity", reasons["unbound"]);
        Assert.Equal("missing-detection", reasons["missing-detection"]); Assert.Equal("missing-detection", reasons["wrong-run"]);
        Assert.Equal("conflicting-detection-time", reasons["conflicting-start"]);
        Assert.Equal("conflicting-scan-time", Assert.Single(report.Detail.Items, sample => sample.MessageIdentity == "m-8").ScanTimingUnavailableReason);
        Assert.Equal("missing-scan-result", reasons["no-received-barcode"]);
    }

    /// <summary>设备测量时间缺失不丢扫码趋势，分页和真实点抽样不改变全体P95及首尾。</summary>
    [Fact]
    public async Task ScanTrendAndQuantilesUseAllSamplesAndKeepRealZero() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        for (var index = 0; index < 205; index++) {
            var detected = at.AddSeconds(index);
            await Detect(db, "a", "run-1", index + 1, detected);
            await Save(db, Measurement("rx-" + index, "m-" + index, "a", "run-1", index + 1, detected.AddMilliseconds(index)) with {
                MeasuredAt = null, OccurredAt = detected.AddSeconds(2) });
        }
        var report = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SCAN", MeasurementPageNumber = 11 }, default);
        Assert.Equal(205, report.ScanTimingSampleCount); Assert.Equal(0, report.MissingScanTimingCount);
        Assert.Equal(102m, report.Detail!.Summary.ScanDuration.Average); Assert.Equal(102m, report.Detail.Summary.ScanDuration.Median);
        Assert.Equal(194m, report.Detail.Summary.ScanDuration.P95); Assert.Equal(0m, report.Detail.Summary.ScanDuration.Minimum);
        Assert.Empty(report.Detail.Trend); Assert.Equal(200, report.Detail.ScanTrend.Count); Assert.True(report.Detail.ScanTrendTruncated);
        Assert.Equal(0m, report.Detail.ScanTrend[0].ScanDurationMilliseconds); Assert.Equal(204m, report.Detail.ScanTrend[^1].ScanDurationMilliseconds);
        Assert.Equal(at, report.Detail.ScanTrend[0].ScanCompletedAt); Assert.Equal(at.AddSeconds(204).AddMilliseconds(204), report.Detail.ScanTrend[^1].ScanCompletedAt);
        Assert.Equal(5, report.Detail.Items.Count);
    }

    /// <summary>只有扫码时间也可比较，来源接收事件可作明确端点，真实零耗时不制造百分比。</summary>
    [Fact]
    public async Task ScanOnlySamplesAreComparableAndSourceEventFallbackIsExplicit() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        foreach (var id in new long[] { 1, 2 }) {
            await Detect(db, "a", "run-1", id, at);
            await Save(db, Measurement("rx-" + id, "m-" + id, "a", "run-1", id, at) with {
                ReceivedAt = null, MeasuredAt = null, WeightGrams = null, LengthMm = null, WidthMm = null, HeightMm = null, VolumeMm3 = null });
        }
        var report = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SCAN", SortBy = "scan-duration" }, default);
        var group = Assert.Single(report.Items); Assert.True(group.IsComparable); Assert.Equal(1, report.ComparableBarcodeCount);
        Assert.Equal(0, group.Weight.Count); Assert.Equal(0, group.Volume.Count); Assert.Equal(2, group.ScanDuration.Count);
        Assert.Equal(0m, group.ScanDuration.Median); Assert.Equal(0m, group.ScanDuration.P95); Assert.Null(group.ScanDuration.SpreadPercent);
        Assert.False(group.ScanDurationDeviates); Assert.All(report.Detail!.Items, sample => Assert.Equal("source-event", sample.ScanTimingBasis));
    }

    /// <summary>接收与绑定的来源包裹号相互矛盾时，不用绑定端点拼接另一票的扫码起点。</summary>
    [Fact]
    public void ConflictingSourceParcelNumbersDoNotCreateScanTiming() {
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var sample = new DwsMeasurementSample { Barcode = "SCAN", SourceInstanceId = "a", SourceRunId = "run-1", SourceParcelId = "2", ParcelId = "10" };
        var received = new ParcelDwsMeasurementSnapshot { Barcode = "SCAN", SourceParcelId = 1, Stage = ParcelProcessingStage.DwsReceived, ReceivedAt = at };
        var bound = received with { SourceParcelId = 2, Stage = ParcelProcessingStage.DwsBound };
        var result = ParcelDwsScanTimingReader.ReceiveEndpoint(sample, [received, bound]);
        Assert.Equal("conflicting-parcel-identity", result.ScanTimingUnavailableReason);
        Assert.Null(result.ScanCompletedAt); Assert.Null(result.ScanDurationMilliseconds);
    }

    /// <summary>生产同款缓存、预算与EF Core分表读取。</summary>
    private static ParcelDwsConsistencyReadService Reader(RelationalParcelTestDatabase db, ParcelDwsConsistencyCache? cache = null) => new(
        db.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), db.Partitions, cache);
    /// <summary>单日测试请求。</summary>
    private static ParcelDwsConsistencyRequest Request(DateTime at) => new() { FromDate = at.Date, ToDate = at.Date };
    /// <summary>具有明确接收时间的独立DWS样本。</summary>
    private static ParcelProcessingRecord Measurement(string id, string message, string source, string run, long? parcel, DateTime at) => new() {
        RecordId = id, PayloadHash = id, MessageIdentity = message, SourceInstanceId = source, SourceRunId = run, SourceParcelId = parcel,
        WorkstationName = "工作台-" + source, Barcode = "SCAN", Stage = ParcelProcessingStage.DwsReceived,
        PartitionTime = at, RecordedAt = at, OccurredAt = at, ReceivedAt = at, MeasuredAt = at,
        WeightGrams = 1000m, LengthMm = 200m, WidthMm = 100m, HeightMm = 100m, VolumeMm3 = 2000000m
    };
    /// <summary>来源检测事实的时间与DWS接收时间分开。</summary>
    private static Task Detect(RelationalParcelTestDatabase db, string source, string run, long parcel, DateTime at, bool? reliable = true) =>
        Save(db, Measurement("detect-" + source + run + parcel, "", source, run, parcel, at) with {
            Stage = ParcelProcessingStage.Detected, HasReliableTimestamp = reliable, MeasuredAt = null, ReceivedAt = null });
    /// <summary>使用真实仓储保存事实并验证结果。</summary>
    private static async Task Save(RelationalParcelTestDatabase db, ParcelProcessingRecord record) {
        var result = await db.Processing.AppendAsync(record, default); Assert.True(result.IsSuccess, result.ErrorMessage);
    }
}
