using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
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
    /// <summary>预建未来周期并自动补齐所有已登记历史周期的索引；底层仍校验建表授权和预演配置。</summary>
    public async Task<object> ExecuteAsync(CancellationToken cancellationToken, int? aheadHours = null) {
        var periods = Plan(aheadHours);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Set<ParcelPartitionCatalogEntry>().AsNoTracking().ToListAsync(cancellationToken);
        // 当前配置粒度只作用于新周期；历史周期使用登记的起止时间，不能重新推断或改写目录。
        var maintenance = existing.Select(entry => new ParcelPartitionPeriod {
            Suffix = entry.Suffix, Start = entry.Start, End = entry.End,
            Granularity = entry.Suffix.Contains('W') ? ParcelTimeShardingGranularity.PerWeek
                : entry.Suffix.Length == 8 ? ParcelTimeShardingGranularity.PerDay : ParcelTimeShardingGranularity.PerMonth
        })
            .Concat(periods).DistinctBy(period => period.Suffix).OrderBy(period => period.Start).ToArray();
        foreach (var period in maintenance) await store.EnsureCreatedAsync(period, cancellationToken, verifyExisting: true);
        return new { plannedSuffixes = periods.Select(x => x.Suffix).ToArray(),
            createdSuffixes = periods.Select(x => x.Suffix).Except(existing.Select(x => x.Suffix)).ToArray(),
            verifiedSuffixes = maintenance.Select(x => x.Suffix).ToArray(),
            completedAtLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified) };
    }
}
