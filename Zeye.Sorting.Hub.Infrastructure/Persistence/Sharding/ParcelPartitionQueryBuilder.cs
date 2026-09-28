using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>把已登记的包裹聚合物理表合并为可组合的只读查询。</summary>
public static class ParcelPartitionQueryBuilder {
    /// <summary>仅使用 EF 模型表名和校验后的目录后缀组装表名，过滤与聚合继续由数据库执行。</summary>
    public static async Task<IQueryable<TEntity>> BuildAsync<TEntity>(
        SortingHubDbContext db,
        ParcelPartitionStore partitions,
        CancellationToken cancellationToken) where TEntity : class {
        var suffixes = await partitions.GetReadSuffixesAsync(cancellationToken);
        return BuildFromSuffixes<TEntity>(db, suffixes);
    }

    /// <summary>包裹的首次入库时间等于分表锚点，仅合并与半开日期窗口重叠的物理周期及历史基础表。</summary>
    public static async Task<IQueryable<Parcel>> BuildParcelsByCreatedTimeAsync(
        SortingHubDbContext db, DateTime fromLocal, DateTime toLocalExclusive, CancellationToken cancellationToken) {
        if (fromLocal == default || toLocalExclusive <= fromLocal
            || fromLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)
            || toLocalExclusive.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("包裹入库时间范围必须是有效的本地半开区间。");
        var suffixes = await db.Set<ParcelPartitionCatalogEntry>().AsNoTracking()
            .Where(period => period.Start < toLocalExclusive && period.End > fromLocal)
            .OrderByDescending(period => period.Start).Select(period => period.Suffix)
            .ToListAsync(cancellationToken);
        suffixes.Add(string.Empty);
        return BuildFromSuffixes<Parcel>(db, suffixes);
    }

    /// <summary>由模型表名与校验过的后缀构建可组合的跨表只读查询。</summary>
    private static IQueryable<TEntity> BuildFromSuffixes<TEntity>(SortingHubDbContext db, IReadOnlyList<string> suffixes) where TEntity : class {
        var entity = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的持久化实体。");
        var tableName = entity.GetTableName()
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的物理表名。");
        var helper = db.GetService<ISqlGenerationHelper>();
        var branches = suffixes.Select(suffix => {
            ParcelPartitionStore.ValidateSuffix(suffix);
            return "SELECT * FROM " + helper.DelimitIdentifier(tableName + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
        });
        return db.Set<TEntity>().FromSqlRaw(string.Join(" UNION ALL ", branches)).AsNoTracking();
    }
}
