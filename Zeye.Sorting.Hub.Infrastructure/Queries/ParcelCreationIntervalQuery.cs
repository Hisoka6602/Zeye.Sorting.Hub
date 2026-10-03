using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>在数据库内计算全部成功入库包裹的相邻创建间隔中位数与最小值，避免下载或截断大批明细。</summary>
internal static class ParcelCreationIntervalQuery {
    /// <summary>合并窗口内分表与历史基础表；按创建时间排序求正间隔，再计算中位数和总体最小值。</summary>
    internal static async Task<CreationIntervalStatistics> ReadAsync(SortingHubDbContext db,
        DateTime fromLocal, DateTime toLocalExclusive, CancellationToken cancellationToken) {
        var suffixes = await db.Set<ParcelPartitionCatalogEntry>().AsNoTracking()
            .Where(period => period.Start < toLocalExclusive && period.End > fromLocal)
            .Select(period => period.Suffix).ToListAsync(cancellationToken);
        suffixes.Add(string.Empty);
        var entity = db.Model.FindEntityType(typeof(Parcel))!;
        var tableName = entity.GetTableName()!;
        var storeObject = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
        var helper = db.GetService<ISqlGenerationHelper>();
        string Column(string property) => helper.DelimitIdentifier(entity.FindProperty(property)!.GetColumnName(storeObject)!);
        var created = Column(nameof(Parcel.CreatedTime));
        var id = Column(nameof(Parcel.Id));
        var detected = Column(nameof(Parcel.DetectedTime));
        var sourceId = Column(nameof(Parcel.SourceParcelId));
        var branches = suffixes.Select(suffix => {
            ParcelPartitionStore.ValidateSuffix(suffix);
            var table = helper.DelimitIdentifier(tableName + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
            return $"SELECT {id} AS ParcelId, {created} AS CreationTime FROM {table} "
                + $"WHERE {created} >= {{0}} AND {created} < {{1}} AND {sourceId} IS NOT NULL AND {detected} IS NOT NULL";
        });
        var difference = db.Database.IsMySql()
            ? "TIMESTAMPDIFF(MICROSECOND, PreviousTime, CreationTime)"
            : db.Database.IsSqlServer()
                ? "DATEDIFF_BIG(MICROSECOND, PreviousTime, CreationTime)"
                : db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
                    ? $"({SqliteMicroseconds("CreationTime")} - {SqliteMicroseconds("PreviousTime")})"
                    : throw new NotSupportedException("当前数据库不支持创建间隔统计。");
        var count = db.Database.IsSqlServer() ? "COUNT_BIG(*)" : "COUNT(*)";
        var sql = $"""
            WITH CreationTimes AS ({string.Join(" UNION ALL ", branches)}),
            OrderedCreations AS (
                SELECT CreationTime, LAG(CreationTime) OVER (ORDER BY CreationTime, ParcelId) AS PreviousTime
                FROM CreationTimes
            ),
            Intervals AS (
                SELECT {difference} AS IntervalMicroseconds FROM OrderedCreations
            ),
            RankedIntervals AS (
                SELECT IntervalMicroseconds,
                    ROW_NUMBER() OVER (ORDER BY IntervalMicroseconds) AS IntervalRank,
                    MIN(IntervalMicroseconds) OVER () AS MinimumIntervalMicroseconds,
                    {count} OVER () AS SampleCount
                FROM Intervals WHERE IntervalMicroseconds > 0
            )
            SELECT AVG(CAST(IntervalMicroseconds AS DECIMAL(22, 4))) / 1000.0 AS MedianIntervalMilliseconds,
                MAX(MinimumIntervalMicroseconds) / 1000.0 AS MinimumIntervalMilliseconds,
                COALESCE(MAX(SampleCount), 0) AS SampleCount
            FROM RankedIntervals WHERE IntervalRank BETWEEN SampleCount / 2.0 AND SampleCount / 2.0 + 1
            """;
        // CTE 直接执行，不能让 EF 在 SQL Server 上把 WITH 包进可组合子查询。
        return (await db.Database.SqlQueryRaw<CreationIntervalStatistics>(sql, fromLocal, toLocalExclusive)
            .ToListAsync(cancellationToken)).Single();
    }

    /// <summary>SQLite 用整数秒和毫秒拼接，避免 julianday 浮点误差破坏毫秒间隔。</summary>
    private static string SqliteMicroseconds(string column) =>
        $"(CAST(strftime('%s', {column}) AS INTEGER) * 1000000 + CAST(substr(strftime('%f', {column}), 4, 3) AS INTEGER) * 1000)";
}
