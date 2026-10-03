using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>在路由外查询物理分表目录与最近一次预建计划。</summary>
public sealed class OperationalPartitionReadService(
    IDbContextFactory<SortingHubDbContext> factory,
    ParcelPartitionStore store,
    ShardingTablePrebuildService prebuild,
    IConfiguration config) {
    /// <summary>返回运维页面所需的白名单分区信息。</summary>
    public async Task<object> GetAsync(CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entries = await db.Set<ParcelPartitionCatalogEntry>().AsNoTracking()
            .OrderByDescending(x => x.Start).Take(200).ToListAsync(cancellationToken);
        var plan = prebuild.GetLastPlan();
        return new {
            provider = config["Persistence:Provider"] ?? "Unknown",
            granularity = config["Persistence:Sharding:Strategy:Time:Granularity"] ?? "Unknown",
            currentSuffix = store.Resolve(DateTime.Now).Suffix,
            allowTableCreation = config.GetValue<bool>("Persistence:Sharding:WriteRouting:AllowTableCreation"),
            creationDryRun = config.GetValue("Persistence:Sharding:WriteRouting:DryRun", true),
            prebuild = plan is null ? null : new {
                plan.GeneratedAtLocal, plan.IsEnabled, plan.IsDryRun, plan.Message,
                plan.PlannedPhysicalTables, plan.MissingPhysicalTables
            },
            entries = entries.Select(x => new { x.Suffix, x.Start, x.End, x.CreatedTime }).ToArray()
        };
    }
}
