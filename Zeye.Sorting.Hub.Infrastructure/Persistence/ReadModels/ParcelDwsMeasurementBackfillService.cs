using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;
using Zeye.Sorting.Hub.Application.Abstractions.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>有界补齐DWS耐久窄表；重启从已提交主键继续，页面请求不触发写库。</summary>
public sealed class ParcelDwsMeasurementBackfillService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) : IParcelDwsMeasurementBackfillService {
    /// <summary>并发补齐竞争只输出低级别异步日志，不隐藏其他数据库失败。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>批次受Oracle和SQL Server参数上限约束。</summary>
    internal const int BatchSize = 512;
    /// <summary>只读取DWS量测及其真实检测起点。</summary>
    private static readonly ParcelProcessingStage[] Stages = [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsReceived, ParcelProcessingStage.DwsBound];

    /// <summary>与耗时索引共用周期进度行和并发版本；不同投影保持独立游标。</summary>
    public async Task<int> RunBatchAsync(CancellationToken token) {
        var catalog = await partitions.GetReadCatalogAsync(token);
        await using var template = await factory.CreateDbContextAsync(token);
        var completed = (await template.Set<ParcelDurationBackfillState>().AsNoTracking().Where(row => row.DwsCompleted)
            .Select(row => row.Suffix).ToListAsync(token)).ToHashSet(StringComparer.Ordinal);
        foreach (var suffix in catalog.Periods.OrderByDescending(period => period.Start).Select(period => period.Suffix).Append(string.Empty)) {
            if (completed.Contains(suffix)) continue;
            try {
                return await template.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                    await using var db = await partitions.CreateContextAsync(suffix, token);
                    await using var transaction = await db.Database.BeginTransactionAsync(token);
                    var state = await db.Set<ParcelDurationBackfillState>().AsTracking().SingleOrDefaultAsync(row => row.Suffix == suffix, token);
                    if (state?.DwsCompleted == true) return 0;
                    if (state is null) { state = new() { Suffix = suffix }; db.Add(state); }
                    var records = await BuildBatch(db.Set<ParcelProcessingRecord>().AsNoTracking(), state.DwsCursor).ToListAsync(token);
                    if (records.Count > 0) {
                        var keys = records.Select(row => row.Key).ToArray();
                        var existing = (await db.Set<ParcelDwsMeasurementSnapshot>().Where(row => keys.Contains(row.Key)).Select(row => row.Key)
                            .ToListAsync(token)).ToHashSet(StringComparer.Ordinal);
                        db.AddRange(records.Where(row => !existing.Contains(row.Key)));
                        state.DwsCursor = records[^1].Key;
                    }
                    state.DwsCompleted = records.Count < BatchSize; state.Revision++; state.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
                    return records.Count;
                });
            }
            catch (DbUpdateConcurrencyException exception) { Logger.Debug(exception, "DWS窄投影补齐发生并发竞争，保留游标等待下一批。"); return 0; }
            catch (DbUpdateException exception) when (DuplicateKeyExceptionDetector.IsDuplicateKeyException(exception)) {
                Logger.Debug(exception, "DWS窄投影已由并发写入保存，保留游标等待下一批。"); return 0;
            }
        }
        return 0;
    }

    /// <summary>按主键读取下一批窄字段，不搬运LOB或使用深分页。</summary>
    internal static IQueryable<ParcelDwsMeasurementSnapshot> BuildBatch(IQueryable<ParcelProcessingRecord> facts, string cursor) {
        facts = facts.Where(row => Stages.Contains(row.Stage));
        if (cursor.Length > 0) facts = facts.Where(row => string.Compare(row.Key, cursor) > 0);
        return facts.OrderBy(row => row.Key).Take(BatchSize).Select(ParcelDwsMeasurementSnapshot.Projection);
    }
}
