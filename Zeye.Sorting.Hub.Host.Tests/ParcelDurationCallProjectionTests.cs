using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证耐久调用样本事务、即时新数据合并、完整来源隔离与清理释放。</summary>
public sealed class ParcelDurationCallProjectionTests {
    /// <summary>后台 SQL 后失败不删除旧样本或错误确认新事实，页面强制刷新仍能看到未归并的新调用。</summary>
    [Fact]
    public async Task FailedProjectionRollsBackAndNewCallsAreVisibleBeforeRecovery() {
        var failure = new DurationProjectionFailureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: failure);
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact("detected", at) with { Stage = ParcelProcessingStage.Detected };
        var parcel = Parcel.CreateDetected(1, first, at);
        parcel.ApplyProcessingRecords([first]);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(parcel); db.Add(first); db.Add(Fact("old", at)); await db.SaveChangesAsync();
        }
        await CompleteBackfillAsync(database);
        var projector = new ParcelDurationCallProjectionService(database.Factory, database.Partitions);
        Assert.Equal(1, await projector.RunBatchAsync(default));
        Assert.Equal(0, await projector.RunBatchAsync(default));
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(Fact("new", at.AddSeconds(1)) with { ElapsedMilliseconds = 250, IsSuccess = false });
            await db.SaveChangesAsync();
        }
        var pending = await ReadAsync(database, at);
        Assert.Equal(2, pending.SampleCount);
        Assert.Equal(1, pending.FailedCount);
        Assert.Contains(pending.Items, row => row.Milliseconds == 0);
        failure.FailNext = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => projector.RunBatchAsync(default));
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            Assert.Single(await db.Set<ParcelDurationCall>().ToListAsync());
            Assert.False((await db.Set<ParcelDurationFact>().SingleAsync(row => row.Key == "new")).Projected);
            Assert.True((await db.Set<ParcelDurationFact>().SingleAsync(row => row.Key == "old")).Projected);
        }
        projector = new(database.Factory, database.Partitions);
        Assert.Equal(1, await projector.RunBatchAsync(default));
        var after = await ReadAsync(database, at);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(pending), JsonSerializer.SerializeToElement(after with { GeneratedAt = pending.GeneratedAt })));
    }

    /// <summary>迟到终态替换同一尝试的未知样本，不叠加计数；外来会话和设备同号事实不串联。</summary>
    [Fact]
    public async Task LateTerminalUpdatesAttemptWithoutDuplicatingOrMixingSources() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 23, 59, 59);
        var first = Fact("detected", at) with { Stage = ParcelProcessingStage.Detected };
        Assert.True((await database.Processing.AppendAsync(first, default)).IsSuccess);
        Assert.True((await database.Processing.AppendAsync(Operation("start", at, "started"), default)).IsSuccess);
        await CompleteBackfillAsync(database);
        var projector = new ParcelDurationCallProjectionService(database.Factory, database.Partitions);
        while (await projector.RunBatchAsync(default) > 0) { }
        var started = await ReadAsync(database, at);
        Assert.Equal(1, started.ObservedCount); Assert.Equal(1, started.UnavailableCount);
        Assert.True((await database.Processing.AppendAsync(Operation("end", at.AddMilliseconds(1500), "completed") with { IsSuccess = true }, default)).IsSuccess);
        var current = await ReadAsync(database, at);
        Assert.Equal(1, current.SampleCount); Assert.Equal(1500, Assert.Single(current.Items).Milliseconds);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            var parent = await db.Set<Parcel>().SingleAsync();
            db.Add(Fact("foreign", at) with { ParcelId = parent.Id, SourceRunId = "foreign-run", ElapsedMilliseconds = 9999 });
            db.Add(Fact("foreign-device", at) with { ParcelId = parent.Id, SourceParcelId = 2, ElapsedMilliseconds = 8888 });
            await db.SaveChangesAsync();
        }
        while (await projector.RunBatchAsync(default) > 0) { }
        var after = await ReadAsync(database, at);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current), JsonSerializer.SerializeToElement(after with { GeneratedAt = current.GeneratedAt })));
        await using var physical = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default);
        Assert.Single(await physical.Set<ParcelDurationCall>().ToListAsync());
        Assert.All(await physical.Set<ParcelDurationFact>().ToListAsync(), row => Assert.True(row.Projected));
    }

    /// <summary>清理同时释放分析投影，审计与原始追溯事实仍按现有规则保留。</summary>
    [Fact]
    public async Task CleanupReleasesDurationProjectionsInTheSameTransaction() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1);
        Assert.True((await database.Processing.AppendAsync(Fact("detected", at) with { Stage = ParcelProcessingStage.Detected }, default)).IsSuccess);
        Assert.True((await database.Processing.AppendAsync(Fact("call", at), default)).IsSuccess);
        var projector = new ParcelDurationCallProjectionService(database.Factory, database.Partitions);
        while (await projector.RunBatchAsync(default) > 0) { }
        var suffix = database.Partitions.Resolve(at).Suffix;
        await using var db = await database.Partitions.CreateContextAsync(suffix, default);
        Assert.Single(await db.Set<ParcelDurationCall>().ToListAsync());
        database.Failure.FailCleanupBatchNumber = 1;
        Assert.False((await database.Parcels.RemoveExpiredAsync(at.AddDays(1), default)).IsSuccess);
        Assert.Single(await db.Set<ParcelDurationCall>().ToListAsync());
        Assert.Single(await db.Set<ParcelDurationFact>().ToListAsync());
        database.Failure.FailCleanupBatchNumber = 0;
        var cleanup = await database.Parcels.RemoveExpiredAsync(at.AddDays(1), default);
        Assert.True(cleanup.IsSuccess, cleanup.ErrorMessage);
        Assert.Empty(await db.Set<ParcelDurationCall>().ToListAsync());
        Assert.Empty(await db.Set<ParcelDurationFact>().ToListAsync());
        Assert.Equal(2, await db.Set<ParcelProcessingRecord>().CountAsync());
    }

    /// <summary>协议阶段、端点和身份固定的测试事实，真实零不伪造为未知。</summary>
    private static ParcelProcessingRecord Fact(string key, DateTime at) => new() {
        Key = key, RecordId = key, ParcelId = 1, SourceInstanceId = "source", SourceRunId = "run", SourceParcelId = 1,
        Stage = ParcelProcessingStage.ScanUploaded, RecordedAt = at, OccurredAt = at, PartitionTime = new(2026, 10, 1),
        PayloadHash = "hash-" + key, ElapsedMilliseconds = 0, HasReliableTimestamp = true, Barcode = "TEST-1",
        RawPayload = "{\"kind\":\"provider-call\",\"category\":\"scan-upload\",\"name\":\"Provider\",\"outcomeLevel\":\"transport\"}"
    };
    /// <summary>同一次操作的开始和终态诊断。</summary>
    private static ParcelProcessingRecord Operation(string key, DateTime at, string outcome) => Fact(key, at) with {
        ElapsedMilliseconds = null, RawPayload = JsonSerializer.Serialize(new { kind = "provider-attempt", category = "Provider", operation = "扫描上传", operationId = "operation", attemptId = "attempt", outcome })
    };
    /// <summary>补齐所有历史周期，使测试覆盖耐久样本读取而非兼容路径。</summary>
    private static async Task CompleteBackfillAsync(RelationalParcelTestDatabase database) {
        var backfill = new ParcelDurationBackfillService(database.Factory, database.Partitions);
        while (await backfill.RunBatchAsync(default) > 0) { }
    }
    /// <summary>强制刷新验证实时统计，不借助进程缓存。</summary>
    private static async Task<ParcelDurationAnalysisResponse> ReadAsync(RelationalParcelTestDatabase database, DateTime at) {
        var reader = new ParcelAnalysisReadService(database.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), database.Partitions);
        return (await reader.ReadAsync(new() { View = "duration", DurationType = "scan-upload", FromDate = at.Date, ToDate = at.Date, RefreshDurationSnapshot = true }, default)).DurationAnalysis!;
    }
}
