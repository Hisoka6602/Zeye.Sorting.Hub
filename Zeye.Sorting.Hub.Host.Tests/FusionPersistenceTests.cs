using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>Fusion处理事实、身份、真实关系事务和物理分表验收。</summary>
public sealed class FusionPersistenceTests {
    /// <summary>验证三种配置和缺省月分表都实际写入预期物理表。</summary>
    [Theory]
    [InlineData(null, "202609")]
    [InlineData("PerMonth", "202609")]
    [InlineData("PerDay", "20260928")]
    [InlineData("PerWeek", "2026W40")]
    public async Task ConfigurationRoutesActualParcelAndRecordTables(string? granularity, string suffix) {
        await using var database = new RelationalParcelTestDatabase(granularity);
        await database.InitializeAsync();
        var result = await database.Processing.AppendAsync(Fact("detection", 1), default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(suffix, result.Value!.PartitionSuffix);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_" + suffix));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ProcessingRecords_" + suffix));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels"));
        var parcel = await database.Parcels.GetByIdAsync(result.Value.ParcelId!.Value, default);
        Assert.NotNull(parcel);
        Assert.Null(parcel.Weight);
        Assert.Null(parcel.DischargeTime);
        Assert.Null(parcel.TargetChuteId);
        Assert.Single(parcel.ProcessingRecords);
    }

    /// <summary>验证跨年ISO周与周日、周一边界。</summary>
    [Theory]
    [InlineData(2020, 12, 31, "2020W53")]
    [InlineData(2021, 1, 3, "2020W53")]
    [InlineData(2021, 1, 4, "2021W01")]
    public void WeeklyBoundaryUsesWeekYear(int year, int month, int day, string expected) {
        var period = ParcelPartitionPeriod.Resolve(new DateTime(year, month, day), ParcelTimeShardingGranularity.PerWeek);
        Assert.Equal(expected, period.Suffix);
        Assert.Equal(DayOfWeek.Monday, period.Start.DayOfWeek);
        Assert.Equal(7, (period.End - period.Start).Days);
    }

    /// <summary>相同条码、NoRead和会话重置不会合并不同过机包裹。</summary>
    [Fact]
    public async Task RepeatedBarcodeAndCounterResetPreserveIndependentIdentity() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var first = await database.Processing.AppendAsync(Fact("one", 1) with { Barcode = "NoRead" }, default);
        var second = await database.Processing.AppendAsync(Fact("two", 2) with { Barcode = "NoRead" }, default);
        var reset = await database.Processing.AppendAsync(Fact("one", 1) with { SourceRunId = "counter-reset" }, default);
        Assert.True(first.IsSuccess && second.IsSuccess && reset.IsSuccess);
        Assert.Equal(3, new[] { first.Value!.ParcelId, second.Value!.ParcelId, reset.Value!.ParcelId }.Distinct().Count());
        Assert.Equal(3, await database.CountPhysicalAsync("Parcels_202609"));
    }

    /// <summary>重启仓储及跨月重复仍命中原凭据，不同内容明确冲突。</summary>
    [Fact]
    public async Task DuplicateAndChangedPayloadAreDurablyDistinguished() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var fact = Fact("stable", 1);
        var original = await database.Processing.AppendAsync(fact, default);
        var restarted = new Zeye.Sorting.Hub.Infrastructure.Repositories.ParcelProcessingRepository(database.Factory, database.Partitions);
        var duplicate = await restarted.AppendAsync(fact with { RecordedAt = fact.RecordedAt.AddMonths(1), PartitionTime = fact.PartitionTime.AddMonths(1) }, default);
        var conflict = await restarted.AppendAsync(fact with { PayloadHash = "changed" }, default);
        Assert.True(original.IsSuccess && duplicate.IsSuccess);
        Assert.True(duplicate.Value!.IsDuplicate);
        Assert.Equal(original.Value!.ParcelId, duplicate.Value.ParcelId);
        Assert.False(conflict.IsSuccess);
        Assert.Equal("ParcelProcessingConflict", conflict.ErrorCode);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
    }

    /// <summary>失败上传和后续重试独立保留，晚到测量不会移动分表或倒退完成状态。</summary>
    [Fact]
    public async Task LifecycleKeepsRawFactsUnitsFailuresAndLateUpdates() {
        await using var database = new RelationalParcelTestDatabase("PerMonth");
        await database.InitializeAsync();
        var detection = Fact("detect", 1);
        var result = await database.Processing.AppendAsync(detection, default);
        Assert.True(result.IsSuccess);
        await database.Processing.AppendAsync(detection with { RecordId = "upload-fail", Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false, ErrorMessage = "timeout", RequestBody = "request", ResponseBody = "error", OccurredAt = detection.OccurredAt.AddSeconds(2) }, default);
        await database.Processing.AppendAsync(detection with { RecordId = "upload-success", Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = true, AttemptNumber = 2, OccurredAt = detection.OccurredAt.AddSeconds(3) }, default);
        await database.Processing.AppendAsync(detection with { RecordId = "route", Stage = ParcelProcessingStage.ChuteAssigned, IsSuccess = true, TargetChuteCode = "D04-01", TaskCode = "TASK-01", IsFallback = true, OccurredAt = detection.OccurredAt.AddSeconds(4) }, default);
        await database.Processing.AppendAsync(detection with { RecordId = "landed", Stage = ParcelProcessingStage.SortingCompleted, IsSuccess = true, ActualChuteCode = "D04-02", OccurredAt = detection.OccurredAt.AddSeconds(5) }, default);
        await database.Processing.AppendAsync(detection with { RecordId = "measurement-late", Stage = ParcelProcessingStage.DwsBound, IsSuccess = true, Barcode = "SAME", WeightGrams = 1234.567m, LengthMm = 300m, WidthMm = null, HeightMm = null, VolumeMm3 = null, VolumetricWeightGrams = 2200m, RawPayload = "raw-dws", BindingMode = "CorrelationId", MessageIdentity = "BATCH-SEQ", FinalSourceParcelId = 1, OccurredAt = detection.OccurredAt.AddSeconds(1), RecordedAt = detection.RecordedAt.AddMonths(1) }, default);
        var parcel = await database.Parcels.GetByIdAsync(result.Value!.ParcelId!.Value, default);
        Assert.NotNull(parcel);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(ApiRequestStatus.Success, parcel.RequestStatus);
        Assert.Equal(1.234567m, parcel.Weight);
        Assert.Null(parcel.Width);
        Assert.Null(parcel.Volume);
        Assert.Equal(2200m, parcel.VolumetricWeightGrams);
        Assert.Equal("D04-01", parcel.TargetChuteCode);
        Assert.Equal("D04-02", parcel.ActualChuteCode);
        Assert.Equal(6, parcel.ProcessingRecords.Count);
        Assert.Contains(parcel.ProcessingRecords, x => x.ErrorMessage == "timeout" && x.RequestBody == "request");
        Assert.Equal("202609", await database.Partitions.LocateAsync(parcel.Id, default));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_202609"));
    }

    /// <summary>没有来源包裹号的DWS原始报文独立持久化并可检索。</summary>
    [Fact]
    public async Task UnboundDwsIsQueryableWithoutCreatingFakeParcel() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var result = await database.Processing.AppendAsync(Fact("unbound", null) with { Stage = ParcelProcessingStage.DwsReceived, RawPayload = "unbound-raw", CandidateSourceParcelId = 12, DecisionReason = "候选不满足窗口" }, default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Null(result.Value!.ParcelId);
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_202609"));
        var records = await database.Processing.GetUnboundAsync(50, default);
        Assert.Single(records);
        Assert.Equal("unbound-raw", records[0].RawPayload);
    }

    /// <summary>事务提交前故障不会留下包裹、处理事实或去重凭据，重试可恢复。</summary>
    [Fact]
    public async Task FailureAfterSqlRollsBackSnapshotRecordAndReceipt() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        database.Failure.FailNextProcessingCommit = true;
        var failed = await database.Processing.AppendAsync(Fact("rollback", 1), default);
        Assert.False(failed.IsSuccess);
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_202609"));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(0, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
        var retry = await database.Processing.AppendAsync(Fact("rollback", 1), default);
        Assert.True(retry.IsSuccess, retry.ErrorMessage);
    }

    /// <summary>跨分表列表分页与游标顺序在数据库中统一执行。</summary>
    [Fact]
    public async Task QueriesMergeDayPartitionsWithStableOrderAndExactCount() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var first = Fact("earlier", 1);
        await database.Processing.AppendAsync(first, default);
        await database.Processing.AppendAsync(Fact("later", 2) with { OccurredAt = first.OccurredAt.AddDays(1), RecordedAt = first.RecordedAt.AddDays(1), PartitionTime = first.RecordedAt.AddDays(1) }, default);
        var page = await database.Parcels.GetPagedAsync(new ParcelQueryFilter(), new PageRequest { PageNumber = 1, PageSize = 1, IncludeTotalCount = true }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items[0].SourceParcelId);
        var next = await database.Parcels.GetCursorPagedAsync(new ParcelQueryFilter(), new CursorPageRequest { PageSize = 1, LastScannedTimeLocal = page.Items[0].ScannedTime, LastId = page.Items[0].Id }, default);
        Assert.Equal(1, next.Items[0].SourceParcelId);
    }

    /// <summary>JSON合同拒绝时区后缀，验证缺失值与非法测量值。</summary>
    [Fact]
    public void ContractPreservesUnknownValuesAndRejectsInvalidMeasurement() {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParcelProcessingRecordRequest>("{\"OccurredAt\":\"2026-09-28T10:00:00+08:00\"}"));
        var request = new ParcelProcessingRecordRequest { RecordId = "test", SourceInstanceId = "dws", SourceRunId = "counter", SourceParcelId = 1, OccurredAt = new(2026, 9, 28, 10, 0, 0), Stage = 0 };
        var record = ParcelProcessingContractMapper.ToDomain(request, request.OccurredAt);
        Assert.Null(record.WeightGrams);
        Assert.Throws<ArgumentException>(() => ParcelProcessingContractMapper.ToDomain(request with { WeightGrams = -1m }, request.OccurredAt));
    }

    /// <summary>更换粒度后新来源使用新分表，已有来源和补发事实保持原分区。</summary>
    [Fact]
    public async Task ChangingGranularityPreservesHistoricalLocationsAndQueries() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var original = await database.Processing.AppendAsync(Fact("monthly", 1), default);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerWeek",
            ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = "true",
            ["Persistence:Sharding:WriteRouting:DryRun"] = "false"
        }).Build();
        var weekly = new ParcelPartitionStore(database.Factory, configuration);
        var writer = new Zeye.Sorting.Hub.Infrastructure.Repositories.ParcelProcessingRepository(database.Factory, weekly);
        var late = await writer.AppendAsync(Fact("late", 1) with { Stage = ParcelProcessingStage.ScanUploaded, RecordedAt = new(2026, 10, 5) }, default);
        var fresh = await writer.AppendAsync(Fact("weekly", 2), default);
        Assert.True(original.IsSuccess && late.IsSuccess && fresh.IsSuccess);
        Assert.Equal(original.Value!.ParcelId, late.Value!.ParcelId);
        Assert.Equal("202609", late.Value.PartitionSuffix);
        Assert.Equal("2026W40", fresh.Value!.PartitionSuffix);
        var page = await database.Parcels.GetPagedAsync(new ParcelQueryFilter(), new PageRequest { IncludeTotalCount = true }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, (await database.Parcels.GetByIdAsync(original.Value.ParcelId!.Value, default))!.ProcessingRecords.Count);
        var analytics = new ParcelAnalyticsReadService(database.Factory, weekly,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())));
        var report = await analytics.GetAsync(new(2026, 9, 28), new(2026, 9, 28), default);
        Assert.Equal(2, report.DetectedCount);
        Assert.Equal(3, report.ProcessingEventCount);
    }

    /// <summary>建表中途失败留下的相同结构可以恢复，重复预建不会覆盖现存数据。</summary>
    [Fact]
    public async Task PartialPhysicalCreationCanBeRetriedWithoutDroppingData() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var period = database.Partitions.Resolve(new(2026, 9, 28));
        await database.Partitions.EnsureCreatedAsync(period, default);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ParcelPartitionCatalog");
            await db.Database.ExecuteSqlRawAsync("DROP TABLE Parcel_ProcessingRecords_202609");
        }
        await database.Partitions.EnsureCreatedAsync(period, default);
        var result = await database.Processing.AppendAsync(Fact("after-recovery", 1), default);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_202609"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
    }

    /// <summary>读取历史只附加明细，管理端保存的快照状态不会被读取操作重置。</summary>
    [Fact]
    public async Task ReadingHistoryDoesNotOverwritePersistedAdministrativeStatus() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var result = await database.Processing.AppendAsync(Fact("detected", 1), default);
        var parcel = (await database.Parcels.GetByIdAsync(result.Value!.ParcelId!.Value, default))!;
        parcel.MarkCompleted(new(2026, 9, 28, 10, 0, 10));
        Assert.True((await database.Parcels.UpdateAsync(parcel, default)).IsSuccess);
        var reloaded = (await database.Parcels.GetByIdAsync(parcel.Id, default))!;
        Assert.Equal(ParcelStatus.Completed, reloaded.Status);
        Assert.Single(reloaded.ProcessingRecords);
    }

    /// <summary>跨表清理真实统计所有分区，守卫默认阻断，显式执行仍保留来源与处理审计。</summary>
    [Fact]
    public async Task CleanupIncludesPhysicalPartitionsAndRespectsGuard() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        await database.Processing.AppendAsync(Fact("first", 1), default);
        await database.Processing.AppendAsync(Fact("second", 2) with { RecordedAt = new(2026, 9, 29) }, default);
        var blocked = await database.Parcels.RemoveExpiredAsync(new(2026, 10, 1), default);
        Assert.True(blocked.IsSuccess, blocked.ErrorMessage);
        Assert.Equal(2, blocked.Value!.PlannedCount);
        Assert.True(blocked.Value.IsBlockedByGuard);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:AllowDangerousActionExecution"] = "true",
            ["Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:DryRun"] = "false"
        }).Build();
        var cleaner = new Zeye.Sorting.Hub.Infrastructure.Repositories.ParcelRepository(database.Factory, configuration, database.Partitions);
        var executed = await cleaner.RemoveExpiredAsync(new(2026, 10, 1), default);
        Assert.True(executed.IsSuccess, executed.ErrorMessage);
        Assert.Equal(2, executed.Value!.ExecutedCount);
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_20260928"));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_20260929"));
        Assert.Equal(2, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }

    /// <summary>条码、图片及接口值对象随聚合共用周期，并在真实关系详情中完整返回。</summary>
    [Fact]
    public async Task RelatedFactsUseSamePhysicalPartitionAsParcel() {
        await using var database = new RelationalParcelTestDatabase("PerWeek");
        await database.InitializeAsync();
        var result = await database.Processing.AppendAsync(Fact("with-details", 1), default);
        var parcel = (await database.Parcels.GetByIdAsync(result.Value!.ParcelId!.Value, default))!;
        parcel.AddBarCodeInfo(new BarCodeInfo { BarCode = "SECONDARY-CODE", BarCodeType = (BarCodeType)0, CapturedTime = new(2026, 9, 28) });
        parcel.AddImageInfo(new ImageInfo { CameraName = "top-camera", RelativePath = "parcel/test.jpg", ImageType = (ImageType)0, CaptureType = (ImageCaptureType)0 });
        parcel.AddApiRequest(new ApiRequestInfo { ApiType = (ApiRequestType)0, RequestStatus = ApiRequestStatus.Failed, RequestUrl = "https://example.invalid/scan", RequestTime = new(2026, 9, 28), RequestBody = "LEGACY-REQUEST", ResponseBody = "LEGACY-FAILURE" });
        Assert.True((await database.Parcels.UpdateAsync(parcel, default)).IsSuccess);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_BarCodeInfos_2026W40"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ImageInfos_2026W40"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ApiRequests_2026W40"));
        var detail = (await database.Parcels.GetByIdAsync(parcel.Id, default))!;
        Assert.Equal("SECONDARY-CODE", Assert.Single(detail.BarCodeInfos).BarCode);
        Assert.Equal("parcel/test.jpg", Assert.Single(detail.ImageInfos).RelativePath);
        Assert.Equal("LEGACY-FAILURE", Assert.Single(detail.ApiRequests).ResponseBody);
        detail.MarkCompleted(new(2026, 9, 28, 10, 0, 3));
        Assert.True((await database.Parcels.UpdateAsync(detail, default)).IsSuccess);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_BarCodeInfos_2026W40"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ImageInfos_2026W40"));
        Assert.Equal(ParcelStatus.Completed, (await database.Parcels.GetByIdAsync(parcel.Id, default))!.Status);
    }

    /// <summary>建立稳定来源身份与本地时间的基础测试事实。</summary>
    private static ParcelProcessingRecord Fact(string recordId, long? sourceParcelId) => new() {
        RecordId = recordId, SourceInstanceId = "fusion-sorter-01", SourceRunId = "counter-session-01", SourceParcelId = sourceParcelId,
        Stage = ParcelProcessingStage.Detected, OccurredAt = new(2026, 9, 28, 10, 0, 0), RecordedAt = new(2026, 9, 28, 10, 0, 0), PartitionTime = new(2026, 9, 28, 10, 0, 0), PayloadHash = "original", IsSuccess = true
    };
}
