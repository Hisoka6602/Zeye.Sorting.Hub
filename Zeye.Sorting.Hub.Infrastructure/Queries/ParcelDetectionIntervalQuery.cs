using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>在数据库内计算来源包裹的相邻检测间隔，避免服务器批量入库节奏污染时效。</summary>
internal static class ParcelDetectionIntervalQuery {
    /// <summary>合并全部入库分表与历史基础表；按窗口内的源端检测时间求正间隔、中位数和总体最小值。</summary>
    internal static async Task<DetectionIntervalStatistics> ReadAsync(SortingHubDbContext db, IReadOnlyList<string> suffixes,
        DateTime fromLocal, DateTime toLocalExclusive, CancellationToken cancellationToken) {
        var entity = db.Model.FindEntityType(typeof(Parcel))!;
        var tableName = entity.GetTableName()!;
        var storeObject = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
        var helper = db.GetService<ISqlGenerationHelper>();
        string Column(string property) => helper.DelimitIdentifier(entity.FindProperty(property)!.GetColumnName(storeObject)!);
        var id = "p." + Column(nameof(Parcel.Id));
        var detected = "p." + Column(nameof(Parcel.DetectedTime));
        var sourceId = "p." + Column(nameof(Parcel.SourceParcelId));
        var branches = suffixes.Select(suffix => {
            ParcelPartitionStore.ValidateSuffix(suffix);
            var table = helper.DelimitIdentifier(tableName + (suffix.Length == 0 ? "" : "_" + suffix), entity.GetSchema());
            return $"SELECT {id} AS ParcelId, {detected} AS DetectionTime FROM {table} p "
                + $"WHERE {detected} >= {{0}} AND {detected} < {{1}} AND {sourceId} IS NOT NULL AND "
                + ParcelTimestampReliabilitySql.ReliableDetection(entity, helper, suffix, db.Database.ProviderName, "p");
        });
        var difference = db.Database.IsMySql()
            ? "TIMESTAMPDIFF(MICROSECOND, PreviousTime, DetectionTime)"
            : db.Database.IsSqlServer()
                ? "DATEDIFF_BIG(MICROSECOND, PreviousTime, DetectionTime)"
                : db.Database.ProviderName == DbProviderNames.Oracle
                    ? OracleMicrosecondsDifference()
                : db.Database.ProviderName == DbProviderNames.SQLite
                    ? $"({SqliteMicroseconds("DetectionTime")} - {SqliteMicroseconds("PreviousTime")})"
                    : throw new NotSupportedException("当前数据库不支持检测间隔统计。");
        var count = db.Database.IsSqlServer() ? "COUNT_BIG(*)" : "COUNT(*)";
        var sql = $"""
            WITH DetectionTimes AS ({string.Join(" UNION ALL ", branches)}),
            OrderedDetections AS (
                SELECT DetectionTime, LAG(DetectionTime) OVER (ORDER BY DetectionTime, ParcelId) AS PreviousTime
                FROM DetectionTimes
            ),
            Intervals AS (
                SELECT {difference} AS IntervalMicroseconds FROM OrderedDetections
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
        return (await db.Database.SqlQueryRaw<DetectionIntervalStatistics>(sql, fromLocal, toLocalExclusive)
            .ToListAsync(cancellationToken)).Single();
    }

    /// <summary>SQLite 用整数秒和毫秒拼接，避免 julianday 浮点误差破坏毫秒间隔。</summary>
    private static string SqliteMicroseconds(string column) =>
        $"(CAST(strftime('%s', {column}) AS INTEGER) * 1000000 + CAST(substr(strftime('%f', {column}), 4, 3) AS INTEGER) * 1000)";

    /// <summary>Oracle 时间戳之差为 INTERVAL；逐项按十进制定点运算保留微秒，不转换为低精度 DATE。</summary>
    private static string OracleMicrosecondsDifference() =>
        "(EXTRACT(DAY FROM (DetectionTime - PreviousTime)) * 86400000000"
        + " + EXTRACT(HOUR FROM (DetectionTime - PreviousTime)) * 3600000000"
        + " + EXTRACT(MINUTE FROM (DetectionTime - PreviousTime)) * 60000000"
        + " + EXTRACT(SECOND FROM (DetectionTime - PreviousTime)) * 1000000)";
}
