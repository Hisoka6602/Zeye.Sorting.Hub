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

    /// <summary>按目录中的单个物理周期构建查询，供有界分表读取使用。</summary>
    public static IQueryable<TEntity> BuildSingle<TEntity>(SortingHubDbContext db, string suffix) where TEntity : class =>
        BuildFromSuffixes<TEntity>(db, [suffix]);

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
    public static IQueryable<TEntity> BuildFromSuffixes<TEntity>(SortingHubDbContext db, IReadOnlyList<string> suffixes) where TEntity : class {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个包裹物理表。", nameof(suffixes));
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

    /// <summary>各物理表先过滤时间并投影统计字段，避免跨表合并物化原文及无关明细；表名和列名只来自模型。</summary>
    /// <typeparam name="TEntity">已映射的持久化实体。</typeparam>
    /// <typeparam name="TReadModel">只包含实体标量属性的查询读模型。</typeparam>
    /// <param name="db">只读上下文。</param>
    /// <param name="suffixes">已登记的物理周期与历史基础表后缀。</param>
    /// <param name="timeProperty">实体中用于时间范围过滤的属性名称。</param>
    /// <param name="fromLocal">本地起始时间，包含边界。</param>
    /// <param name="toLocal">本地终止时间，默认不包含边界。</param>
    /// <param name="includeEnd">是否包含终止时间，仅用于实时观察窗口。</param>
    public static IQueryable<TReadModel> BuildTimeRangeReadModel<TEntity, TReadModel>(SortingHubDbContext db,
        IReadOnlyList<string> suffixes, string timeProperty, DateTime fromLocal, DateTime toLocal, bool includeEnd = false)
        where TEntity : class where TReadModel : class {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个物理表。", nameof(suffixes));
        if (toLocal <= fromLocal || fromLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)
            || toLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("统计时间范围必须是有效的本地区间。");
        var entity = db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"未配置 {typeof(TEntity).Name} 的持久化实体。");
        var tableName = entity.GetTableName()!;
        var storeObject = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
        var helper = db.GetService<ISqlGenerationHelper>();
        // 步骤1：显式标量投影遵循模型列名，不能把拥有关系、原文或所有实体列带进 UNION。
        var columns = typeof(TReadModel).GetProperties().Select(property => {
            var mapped = entity.FindProperty(property.Name)
                ?? throw new InvalidOperationException($"{typeof(TReadModel).Name}.{property.Name} 不是可统计的标量属性。");
            return helper.DelimitIdentifier(mapped.GetColumnName(storeObject)!) + " AS " + helper.DelimitIdentifier(property.Name);
        }).ToArray();
        var time = entity.FindProperty(timeProperty)
            ?? throw new ArgumentException("统计时间属性未映射。", nameof(timeProperty));
        if (time.ClrType != typeof(DateTime) && time.ClrType != typeof(DateTime?))
            throw new ArgumentException("统计时间属性必须是日期时间。", nameof(timeProperty));
        var timeColumn = helper.DelimitIdentifier(time.GetColumnName(storeObject)!);
        // 步骤2：范围条件放进每个分支，让每张物理表的时间索引参与查询；时间值始终参数化。
        var branches = suffixes.Distinct(StringComparer.Ordinal).Select(suffix => {
            ParcelPartitionStore.ValidateSuffix(suffix);
            var table = helper.DelimitIdentifier(tableName + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
            return $"SELECT {string.Join(", ", columns)} FROM {table} WHERE {timeColumn} >= {{0}} AND {timeColumn} {(includeEnd ? "<=" : "<")} {{1}}";
        });
        return db.Database.SqlQueryRaw<TReadModel>(string.Join(" UNION ALL ", branches), fromLocal, toLocal);
    }
}
