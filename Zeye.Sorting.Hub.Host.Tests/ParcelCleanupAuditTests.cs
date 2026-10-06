using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>跨分表删除与精简审计在关系事务中一起提交，失败保留准确进度。</summary>
public sealed class ParcelCleanupAuditTests {
    /// <summary>测试与生产使用同一序列化合同，避免反复构造选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>默认执行真实删除，并在下一次清理及服务重建后仍可查询旧操作。</summary>
    [Fact]
    public async Task DefaultCleanupPreservesActorSummaryAndSourceFactsAcrossPartitions() {
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
        var detail = JsonSerializer.SerializeToElement(await new ParcelCleanupHistoryService(db.Factory).DetailAsync(result.Value.CleanupRecordId!, default), JsonOptions);
        Assert.False(detail.TryGetProperty("items", out _));
        Assert.Equal(2, detail.GetProperty("record").GetProperty("executedCount").GetInt32());
        Assert.Equal(ParcelCleanupAudit.SummaryStorageFormat, detail.GetProperty("record").GetProperty("storageFormat").GetString());
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
        var audit = JsonSerializer.Deserialize<ParcelCleanupAudit>(header.Json, JsonOptions)!;
        Assert.Equal("failed", audit.Status); Assert.Equal(committed, audit.ExecutedCount);
        var details = await context.Set<ManagedDocument>().Where(x => x.Key.StartsWith(ParcelCleanupAudit.BatchPrefix(audit.Id))).Select(x => x.Json).ToListAsync();
        Assert.Equal(committed, details.Sum(x => JsonSerializer.Deserialize<ParcelCleanupBatchAudit>(x, JsonOptions)!.DeletedCount));
        Assert.All(details, x => Assert.DoesNotContain("CLEANUP-", x));
    }
    /// <summary>一万票清理只留下小于4KB的操作和批次汇总，存储不随业务字段长度扩大。</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(10000, 10)]
    public async Task CleanupDoesNotPersistPerParcelData(int count, int batches) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var parcels = Enumerable.Range(1, count).Select(x => Parcel.CreateDetected(100000 + x, Fact(x, new(2026, 9, 28)), new(2026, 9, 28))).ToArray();
        Assert.True((await db.Parcels.AddRangeAsync(parcels, default)).IsSuccess);
        var result = await db.Parcels.RemoveExpiredAsync(new(2026, 10, 1), default);
        Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(count, result.Value.ExecutedCount);
        Assert.Equal(0, await db.CountPhysicalAsync("Parcels_202609"));
        await using var context = await db.Factory.CreateDbContextAsync();
        var permanent = await context.Set<ManagedDocument>().Where(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix) || x.Key.StartsWith("parcel-cleanup-batch:")).ToListAsync();
        Assert.Equal(batches + 1, permanent.Count);
        Assert.InRange(permanent.Sum(x => System.Text.Encoding.UTF8.GetByteCount(x.Json)), 1, 4096);
        Assert.All(permanent, x => { Assert.DoesNotContain("CLEANUP-", x.Json); Assert.DoesNotContain("cleanup-sorter", x.Json); Assert.DoesNotContain("清理测试工作台", x.Json); });
    }
    /// <summary>旧批次载荷不再进入详情读取，不因明细损坏或庞大载荷阻塞操作查询。</summary>
    [Fact]
    public async Task LegacyDetailReadsOnlyPermanentOperationHeader() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var audit = new ParcelCleanupAudit { Id = Guid.NewGuid().ToString("N"), Operator = new("operator", "admin", "管理员", "", ""),
            CreatedBefore = new(2026, 10, 1), Decision = "execute", Status = "completed", PlannedCount = 1000, ExecutedCount = 1000, BatchCount = 1, CompensationBoundary = "旧版清单" };
        await using var context = await db.Factory.CreateDbContextAsync();
        context.Add(new ManagedDocument { Key = ParcelCleanupAudit.Prefix + audit.Id, Json = JsonSerializer.Serialize(audit, JsonOptions), Revision = 1 });
        context.Add(new ManagedDocument { Key = ParcelCleanupAudit.BatchPrefix(audit.Id) + "0001", Json = "损坏的旧明细，不应被详情查询加载", Revision = 1 });
        await context.SaveChangesAsync();
        var detail = JsonSerializer.SerializeToElement(await new ParcelCleanupHistoryService(db.Factory).DetailAsync(audit.Id, default), JsonOptions);
        Assert.False(detail.TryGetProperty("items", out _)); Assert.DoesNotContain("旧明细", detail.GetRawText());
        Assert.Equal(1000, detail.GetProperty("record").GetProperty("executedCount").GetInt32());
        Assert.Null(await new ParcelCleanupHistoryService(db.Factory).DetailAsync(Guid.NewGuid().ToString("N"), default));
    }
    /// <summary>稳定的来源身份、时间、条码与工作台事实。</summary>
    internal static ParcelProcessingRecord Fact(long sourceId, DateTime date) => new() {
        RecordId = "cleanup-fixture-" + sourceId, SourceInstanceId = "cleanup-sorter", SourceRunId = "cleanup-session", SourceParcelId = sourceId,
        Stage = Zeye.Sorting.Hub.Domain.Enums.Parcels.ParcelProcessingStage.Detected, OccurredAt = date, RecordedAt = date, PartitionTime = date,
        PayloadHash = "original", IsSuccess = true, Barcode = "CLEANUP-" + sourceId, WorkstationName = "清理测试工作台"
    };
}
