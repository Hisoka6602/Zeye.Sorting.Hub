using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>批次事实的真实事务、逐记录不可变身份和包裹最终状态回归。</summary>
public sealed class FusionParcelBatchPersistenceTests {
    /// <summary>完整闭环保留全部事实，只执行一次保存，并正确区分接口成功与实际落格。</summary>
    [Fact]
    public async Task LifecycleBatchUsesSingleAtomicSaveAndKeepsAllEvidence() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var facts = Lifecycle();
        var results = await database.Processing.AppendBatchAsync(facts, default);
        Assert.All(results, result => Assert.True(result.IsSuccess, result.ErrorMessage));
        Assert.Equal(1, database.Failure.ProcessingWrites);
        Assert.Single(results.Select(result => result.Value!.ParcelId).Distinct());
        Assert.Equal(facts.Length, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(facts.Length, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
        var parcel = (await database.Parcels.GetByIdAsync(results[0].Value!.ParcelId!.Value, default))!;
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(1.234m, parcel.Weight);
        Assert.Equal("BARCODE", parcel.BarCodes);
        Assert.Equal("0013", parcel.TargetChuteCode);
        Assert.Equal("14", parcel.ActualChuteCode);
        Assert.Contains(parcel.ProcessingRecords, record => record.Stage == ParcelProcessingStage.ScanUploaded && record.IsSuccess == false);
    }

    /// <summary>SQL执行后的故障回滚所有事实、凭据、定位和包裹，完整重放可以恢复。</summary>
    [Fact]
    public async Task FailureAfterSqlRollsBackWholeBatchAndRetryRestoresIt() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        database.Failure.FailNextProcessingCommit = true;
        var failed = await database.Processing.AppendBatchAsync(Lifecycle(), default);
        Assert.All(failed, result => Assert.False(result.IsSuccess));
        foreach (var table in new[] { "Parcels_202609", "Parcel_ProcessingRecords_202609", "ParcelProcessingReceipts", "ParcelLocations" })
            Assert.Equal(0, await database.CountPhysicalAsync(table));
        var retried = await database.Processing.AppendBatchAsync(Lifecycle(), default);
        Assert.All(retried, result => Assert.True(result.IsSuccess, result.ErrorMessage));
        Assert.Equal(6, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_202609"));
    }

    /// <summary>持久凭据和同批重复分别判定，相同记录不同内容不能覆盖有效事实。</summary>
    [Fact]
    public async Task MixedKnownNewDuplicateAndConflictingFactsRemainIndependent() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detected = Fact("detected", ParcelProcessingStage.Detected);
        await database.Processing.AppendAsync(detected, default);
        var measured = Fact("measure", ParcelProcessingStage.DwsBound) with { WeightGrams = 1000m, Barcode = "BARCODE", IsSuccess = true };
        var results = await database.Processing.AppendBatchAsync([
            measured, detected with { PayloadHash = "changed" }, measured, measured with { PayloadHash = "changed" }], default);
        Assert.True(results[0].IsSuccess);
        Assert.Equal("ParcelProcessingConflict", results[1].ErrorCode);
        Assert.True(results[2].IsSuccess && results[2].Value!.IsDuplicate);
        Assert.Equal("ParcelProcessingConflict", results[3].ErrorCode);
        Assert.Equal(2, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(2, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
        var parcel = (await database.Parcels.GetByIdAsync(results[0].Value!.ParcelId!.Value, default))!;
        Assert.Equal(1m, parcel.Weight);
    }

    /// <summary>第二条检测不得重置同票，其他有效测量仍被保存。</summary>
    [Fact]
    public async Task SecondDetectionIsRejectedWithoutDroppingValidBatchFacts() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var results = await database.Processing.AppendBatchAsync([
            Fact("first", ParcelProcessingStage.Detected), Fact("second", ParcelProcessingStage.Detected),
            Fact("measure", ParcelProcessingStage.DwsBound) with { WeightGrams = 1234m, IsSuccess = true }], default);
        Assert.True(results[0].IsSuccess && results[2].IsSuccess);
        Assert.Equal("ParcelSourceConflict", results[1].ErrorCode);
        Assert.Equal(2, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
        Assert.Equal(2, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_202609"));
    }

    /// <summary>跨月重复与迟到测量仍固定首次分表，完结状态不倒退。</summary>
    [Fact]
    public async Task CrossMonthReplayAndLateMeasurementsKeepFirstPartitionAndCompletion() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var original = await database.Processing.AppendBatchAsync(Lifecycle(), default);
        var duplicates = await database.Processing.AppendBatchAsync(Lifecycle().Select(record => record with {
            RecordedAt = record.RecordedAt.AddMonths(1), PartitionTime = record.PartitionTime.AddMonths(1) }).ToArray(), default);
        Assert.All(duplicates, result => { Assert.True(result.IsSuccess && result.Value!.IsDuplicate); Assert.Equal("202609", result.Value.PartitionSuffix); });
        var late = Fact("late", ParcelProcessingStage.DwsBound, 4) with { WeightGrams = 2000m, IsSuccess = true,
            RecordedAt = new(2026, 10, 1, 10, 0, 0), PartitionTime = new(2026, 10, 1, 10, 0, 0) };
        var results = await database.Processing.AppendBatchAsync([late, late], default);
        Assert.True(results[0].IsSuccess && results[1].Value!.IsDuplicate);
        var parcel = (await database.Parcels.GetByIdAsync(original[0].Value!.ParcelId!.Value, default))!;
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Equal(2m, parcel.Weight);
        Assert.Equal("202609", await database.Partitions.LocateAsync(parcel.Id, default));
        Assert.Equal(7, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
    }

    /// <summary>不允许把不同来源、编号会话、包裹或未关联事实捆绑成同一包裹事务。</summary>
    [Theory]
    [InlineData("source")]
    [InlineData("run")]
    [InlineData("parcel")]
    [InlineData("unbound")]
    public async Task BatchCannotMixParcelIdentity(string field) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var first = Fact("one", ParcelProcessingStage.Detected);
        var second = Fact("two", ParcelProcessingStage.DwsBound);
        second = field switch { "source" => second with { SourceInstanceId = "other" }, "run" => second with { SourceRunId = "other" },
            "parcel" => second with { SourceParcelId = 2 }, _ => second with { SourceParcelId = null } };
        await Assert.ThrowsAsync<ArgumentException>(() => database.Processing.AppendBatchAsync([first, second], default));
        Assert.Equal(0, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }

    /// <summary>构造接口失败、重试成功及落格不符的完整审计历史。</summary>
    private static ParcelProcessingRecord[] Lifecycle() => [
        Fact("detect", ParcelProcessingStage.Detected),
        Fact("measurement", ParcelProcessingStage.DwsBound, 1) with { Barcode = "BARCODE", WeightGrams = 1234m, IsSuccess = true },
        Fact("upload-failed", ParcelProcessingStage.ScanUploaded, 2) with { IsSuccess = false },
        Fact("upload-success", ParcelProcessingStage.ScanUploaded, 3) with { IsSuccess = true, AttemptNumber = 2 },
        Fact("route", ParcelProcessingStage.ChuteAssigned, 4) with { TargetChuteCode = "0013", IsSuccess = true },
        Fact("landed", ParcelProcessingStage.SortingCompleted, 5) with { ActualChuteCode = "14", IsSuccess = true } ];

    /// <summary>批次路径同样拒绝遗留主键碰撞，不能只依赖旧的逐条仓储检查。</summary>
    [Fact]
    public async Task LegacyBaseCollisionRejectsEveryNewBatchRecord() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var detection = await database.Processing.AppendBatchAsync(Lifecycle(), default);
        var id = detection[0].Value!.ParcelId!.Value;
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            // 使用另一设备来源的同一个确定性哈希编号作为遗留基础表包裹。
            db.Add(Parcel.CreateDetected(id, Fact("legacy", ParcelProcessingStage.Detected) with { SourceInstanceId = "legacy" }, DateTime.Now));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ParcelProcessingReceipts");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Parcel_ProcessingRecords_202609");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Parcels_202609");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ParcelLocations");
        }
        var rejected = await database.Processing.AppendBatchAsync(Lifecycle(), default);
        Assert.All(rejected, result => Assert.Equal("ParcelSourceConflict", result.ErrorCode));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels"));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_202609"));
        Assert.Equal(0, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }

    /// <summary>生成稳定来源身份与不可变记录编号。</summary>
    private static ParcelProcessingRecord Fact(string id, ParcelProcessingStage stage, int seconds = 0) => new() {
        RecordId = id, SourceInstanceId = "batch-fusion", SourceRunId = "batch-run", SourceParcelId = 1,
        Stage = stage, FinalSourceParcelId = stage == ParcelProcessingStage.DwsBound ? 1 : null,
        OccurredAt = new DateTime(2026, 9, 28, 10, 0, 0).AddSeconds(seconds),
        RecordedAt = new(2026, 9, 28, 10, 0, 0), PartitionTime = new(2026, 9, 28, 10, 0, 0), PayloadHash = id };
}
