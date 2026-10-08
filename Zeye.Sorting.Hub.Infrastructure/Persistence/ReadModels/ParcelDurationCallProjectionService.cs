using System.Data;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>后台归并一次调用的多条诊断；事务替换样本并确认已消费事实，页面和摄入链路不执行归并写库。</summary>
public sealed class ParcelDurationCallProjectionService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) {
    /// <summary>一次最多归并256个包裹，主键批次保持所有提供器参数限额以内。</summary>
    internal const int BatchSize = 256;

    /// <summary>按实际物理分表查找待归并事实，当前周期优先，重启会自动接续。</summary>
    public async Task<int> RunBatchAsync(CancellationToken cancellationToken) {
        var catalog = await partitions.GetReadCatalogAsync(cancellationToken);
        await using var template = await factory.CreateDbContextAsync(cancellationToken);
        foreach (var suffix in catalog.Periods.OrderByDescending(period => period.Start).Select(period => period.Suffix).Append(string.Empty)) {
            var strategy = template.Database.CreateExecutionStrategy();
            var changed = await strategy.ExecuteAsync(async () => {
                await using var db = await partitions.CreateContextAsync(suffix, cancellationToken);
                var ids = await db.Set<ParcelDurationFact>().AsNoTracking().Where(row => !row.Projected && row.ParcelId != null)
                    .Select(row => row.ParcelId!.Value).Distinct().Order().Take(BatchSize).ToArrayAsync(cancellationToken);
                if (ids.Length == 0) return 0;
                await using var transaction = await db.Database.BeginTransactionAsync(
                    db.Database.ProviderName is DbProviderNames.SqlServer or DbProviderNames.Oracle ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken);
                // EF 无改值更新获得包裹行锁，跨 Hub 实例按相同包裹串行投影，禁止旧历史覆盖较新结果。
                await db.Set<Parcel>().Where(row => ids.Contains(row.Id)).Select(row => new { row.Id, row.BarCodes })
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.BarCodes, row => row.BarCodes), cancellationToken);
                var parents = await db.Set<Parcel>().AsNoTracking().Where(row => ids.Contains(row.Id))
                    .Select(row => new { row.Id, row.SourceInstanceId, row.SourceRunId, row.SourceParcelId }).ToDictionaryAsync(row => row.Id, cancellationToken);
                var facts = await db.Set<ParcelDurationFact>().AsNoTracking().Where(row => row.ParcelId != null && ids.Contains(row.ParcelId.Value)).ToListAsync(cancellationToken);
                var events = facts.Where(row => parents.TryGetValue(row.ParcelId!.Value, out var parent) && parent.SourceInstanceId == row.SourceInstanceId
                    && parent.SourceRunId == row.SourceRunId && parent.SourceParcelId == row.SourceParcelId).Select(row => new ParcelDurationCallEvent(row)).ToArray();
                await db.Set<ParcelDurationCall>().Where(row => ids.Contains(row.ParcelId)).ExecuteDeleteAsync(cancellationToken);
                db.AddRange(ParcelDurationObservationBuilder.Build(events));
                await db.SaveChangesAsync(cancellationToken);
                // 只确认这次实际读取的键。读后并发提交的新事实保持待处理，不丢失迟到响应或重试。
                foreach (var keys in facts.Where(row => !row.Projected).Select(row => row.Key).Chunk(ParcelDurationBackfillService.BatchSize))
                    await db.Set<ParcelDurationFact>().Where(row => keys.Contains(row.Key)).ExecuteUpdateAsync(update => update.SetProperty(row => row.Projected, true), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ids.Length;
            });
            if (changed > 0) return changed;
        }
        return 0;
    }
}
