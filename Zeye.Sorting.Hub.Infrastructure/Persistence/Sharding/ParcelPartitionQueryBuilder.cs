using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
        SortingHubDbContext db, ParcelPartitionStore partitions, DateTime fromLocal, DateTime toLocalExclusive, CancellationToken cancellationToken) {
        if (fromLocal == default || toLocalExclusive <= fromLocal
            || fromLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)
            || toLocalExclusive.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("包裹入库时间范围必须是有效的本地半开区间。");
        var catalog = await partitions.GetReadCatalogAsync(cancellationToken);
        var suffixes = catalog.Periods.Where(period => period.Start < toLocalExclusive && period.End > fromLocal)
            .Select(period => period.Suffix).Append(string.Empty).ToArray();
        return BuildFromSuffixes<Parcel>(db, suffixes);
    }

    /// <summary>由模型表名与校验过的后缀构建可组合的跨表只读查询。</summary>
    public static IQueryable<TEntity> BuildFromSuffixes<TEntity>(SortingHubDbContext db, IReadOnlyList<string> suffixes) where TEntity : class {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个包裹物理表。", nameof(suffixes));
        if (suffixes.Count == 1 && suffixes[0] == db.ParcelPartitionSuffix) return db.Set<TEntity>().AsNoTracking();
        var entity = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的持久化实体。");
        var tableName = entity.GetTableName()
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的物理表名。");
        var helper = db.GetService<ISqlGenerationHelper>();
        // 基础表的迁移与新分表的模型建表可能产生不同的物理列顺序；UNION ALL 必须按列名对齐。
        var storeObject = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
        var columns = entity.GetProperties()
            .Select(property => property.GetColumnName(storeObject))
            .Where(column => column is not null)
            .Distinct(StringComparer.Ordinal)
            .Select(column => helper.DelimitIdentifier(column!))
            .ToArray();
        if (columns.Length == 0) throw new InvalidOperationException($"{typeof(TEntity).Name} 没有可查询的物理列。");
        var projection = string.Join(", ", columns);
        var branches = suffixes.Select(suffix => {
            ParcelPartitionStore.ValidateSuffix(suffix);
            return "SELECT " + projection + " FROM " + helper.DelimitIdentifier(tableName + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
        });
        return db.Set<TEntity>().FromSqlRaw(string.Join(" UNION ALL ", branches)).AsNoTracking();
    }

}
