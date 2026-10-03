using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>跨分表删除与永久清单在关系事务中一起提交，失败保留准确进度。</summary>
public sealed class ParcelCleanupAuditTests {
    /// <summary>默认执行真实删除，并在下一次清理及服务重建后仍可查询旧操作。</summary>
    [Fact]
    public async Task DefaultCleanupPreservesActorItemsAndSourceFactsAcrossPartitions() {
        await using var db = new RelationalParcelTestDatabase("PerDay"); await db.InitializeAsync();
        await db.Processing.AppendAsync(Fact(1, new(2026, 9, 28)), default);
        await db.Processing.AppendAsync(Fact(2, new(2026, 9, 29)), default);
        await db.Processing.AppendAsync(Fact(3, new(2026, 10, 1)), default);
        var actor = new ParcelCleanupOperator("user-id", "admin", "管理员", "127.0.0.1", "trace-test");
        var result = await db.Parcels.RemoveExpiredAsync(new(2026, 10, 1), default, actor);
        Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(2, result.Value.ExecutedCount);
        Assert.Equal(0, await db.CountPhysicalAsync("Parcels_20260928")); Assert.Equal(1, await db.CountPhysicalAsync("Parcels_20261001"));
        Assert.Equal(3, await db.CountPhysicalAsync("ParcelProcessingReceipts")); Assert.Equal(1, await db.CountPhysicalAsync("Parcel_ProcessingRecords_20260928"));
        await db.Parcels.RemoveExpiredAsync(new(2026, 10, 2), default, actor);
        var detail = JsonSerializer.SerializeToElement(await new ParcelCleanupHistoryService(db.Factory).DetailAsync(result.Value.CleanupRecordId!, 1, 10, "", default), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2, detail.GetProperty("items").GetArrayLength());
        Assert.Equal("admin", detail.GetProperty("record").GetProperty("operator").GetProperty("account").GetString());
        Assert.Equal("completed", detail.GetProperty("record").GetProperty("status").GetString());
    }
    /// <summary>批次审计写入失败时，已经执行的 SQL 删除必须一起回滚。</summary>
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(1001, 2, 1000)]
    public async Task AuditFailureRollsBackItsBatchAndKeepsPriorCommittedProgress(int count, int failedBatch, int committed) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var parcels = Enumerable.Range(1, count).Select(x => Parcel.CreateDetected(100000 + x, Fact(x, new(2026, 9, 28)), new(2026, 9, 28))).ToArray();
        Assert.True((await db.Parcels.AddRangeAsync(parcels, default)).IsSuccess);
        db.Failure.FailCleanupBatchNumber = failedBatch;
        var result = await db.Parcels.RemoveExpiredAsync(new(2026, 10, 1), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(count - committed, await db.CountPhysicalAsync("Parcels_202609"));
        await using var context = await db.Factory.CreateDbContextAsync();
        var header = await context.Set<ManagedDocument>().SingleAsync(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix));
        var audit = JsonSerializer.Deserialize<ParcelCleanupAudit>(header.Json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("failed", audit.Status); Assert.Equal(committed, audit.ExecutedCount);
        var details = await context.Set<ManagedDocument>().Where(x => x.Key.StartsWith(ParcelCleanupAudit.BatchPrefix(audit.Id))).Select(x => x.Json).ToListAsync();
        Assert.Equal(committed, details.Sum(x => JsonSerializer.Deserialize<ParcelCleanupDeletedItem[]>(x, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Length));
    }
    /// <summary>稳定的来源身份、时间、条码与工作台事实。</summary>
    internal static ParcelProcessingRecord Fact(long sourceId, DateTime date) => new() {
        RecordId = "cleanup-fixture-" + sourceId, SourceInstanceId = "cleanup-sorter", SourceRunId = "cleanup-session", SourceParcelId = sourceId,
        Stage = Zeye.Sorting.Hub.Domain.Enums.Parcels.ParcelProcessingStage.Detected, OccurredAt = date, RecordedAt = date, PartitionTime = date,
        PayloadHash = "original", IsSuccess = true, Barcode = "CLEANUP-" + sourceId, WorkstationName = "清理测试工作台"
    };
}
