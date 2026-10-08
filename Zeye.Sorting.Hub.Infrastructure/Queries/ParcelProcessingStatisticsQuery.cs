using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>使用 EF Core LINQ 在物理分表内一次条件聚合，每表只返回一行，不物化事件明细。</summary>
internal static class ParcelProcessingStatisticsQuery {
    /// <summary>保留全部历史分表及基础表，按实际发生时间统计晚到事实和未绑定消息。</summary>
    internal static async Task<ParcelProcessingStatisticsTotals> ReadAsync(SortingHubDbContext db,
        IReadOnlyList<string> suffixes, DateTime fromLocal, DateTime toLocalExclusive, CancellationToken cancellationToken) {
        await using var processing = ParcelPartitionReadContext<ParcelProcessingStatisticsRow>.Create<ParcelProcessingRecord>(db, suffixes);
        var rows = await BuildQuery(processing, suffixes, fromLocal, toLocalExclusive).ToListAsync(cancellationToken);
        return new() { Count = rows.Sum(row => row.Count), Failed = rows.Sum(row => row.Failed),
            UnboundDws = rows.Sum(row => row.UnboundDws) };
    }

    /// <summary>常量分组避免按成功状态整理大量索引项，条件求和保留未知状态，空窗口自然为零。</summary>
    internal static IQueryable<ParcelProcessingStatisticsTotals> BuildQuery(ParcelPartitionReadContext<ParcelProcessingStatisticsRow> processing,
        IReadOnlyList<string> suffixes, DateTime fromLocal, DateTime toLocalExclusive) {
        if (suffixes.Count == 0) throw new ArgumentException("至少提供一个物理分表。", nameof(suffixes));
        if (fromLocal == default || toLocalExclusive <= fromLocal
            || fromLocal.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)
            || toLocalExclusive.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified))
            throw new ArgumentException("统计时间范围必须是有效的本地半开区间。");
        var branches = suffixes.Distinct(StringComparer.Ordinal).Select(suffix => {
            var events = processing.Query([suffix], nameof(ParcelProcessingRecord.OccurredAt), fromLocal, toLocalExclusive, false);
            // 无外层 Take(1) 或独立标量子查询，每个物理表一次检索并直接累加三项计数。
            return events.GroupBy(_ => 1).Select(group => new ParcelProcessingStatisticsTotals {
                Count = group.LongCount(),
                Failed = group.Sum(row => row.IsSuccess == false ? 1L : 0L),
                UnboundDws = group.Sum(row => row.ParcelId == null
                    && (row.Stage == ParcelProcessingStage.DwsReceived || row.Stage == ParcelProcessingStage.DwsBound) ? 1L : 0L)
            });
        });
        return branches.Aggregate((left, right) => left.Concat(right));
    }
}
