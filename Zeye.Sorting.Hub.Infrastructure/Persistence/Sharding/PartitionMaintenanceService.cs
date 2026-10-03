using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
/// <summary>按当前粒度预建包裹主表及关联表，保留现有目录和业务数据。</summary>
public sealed class PartitionMaintenanceService(ParcelPartitionStore store, IDbContextFactory<SortingHubDbContext> factory, IOptions<ShardingPrebuildOptions> options) {
    /// <summary>返回有界窗口内的当前及下一周期。</summary>
    public IReadOnlyList<ParcelPartitionPeriod> Plan(int? aheadHours = null) {
        var first = store.Resolve(DateTime.Now);
        var last = store.Resolve(DateTime.Now.AddHours(Math.Clamp(aheadHours ?? options.Value.PrebuildAheadHours, 1, 168)));
        if (last.Start == first.Start) last = store.Resolve(first.End);
        var periods = new List<ParcelPartitionPeriod>();
        for (var period = first; period.Start <= last.Start; period = store.Resolve(period.End)) periods.Add(period);
        return periods;
    }
    /// <summary>执行明确选择的预建操作；底层仍校验建表授权和预演配置。</summary>
    public async Task<object> ExecuteAsync(CancellationToken cancellationToken, int? aheadHours = null) {
        var periods = Plan(aheadHours);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Set<ParcelPartitionCatalogEntry>().Select(x => x.Suffix).ToListAsync(cancellationToken);
        foreach (var period in periods) await store.EnsureCreatedAsync(period, cancellationToken, verifyExisting: true);
        return new { plannedSuffixes = periods.Select(x => x.Suffix).ToArray(), createdSuffixes = periods.Select(x => x.Suffix).Except(existing).ToArray(), completedAtLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified) };
    }
}
