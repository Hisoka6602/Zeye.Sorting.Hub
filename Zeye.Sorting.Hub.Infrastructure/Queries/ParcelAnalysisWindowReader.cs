using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>按已有时间索引拆分报表读取，避免长时间持有返回十多万行的单个读取器。</summary>
internal static class ParcelAnalysisWindowReader {
    /// <summary>窗口目标行数；保留全部统计总体，不作为截断上限。</summary>
    internal const int TargetRows = 4096;

    /// <summary>半开窗口互不重叠；所有提供器使用整毫秒边界，不引入驱动时间精度舍入。</summary>
    internal static IQueryable<T> Window<T>(IQueryable<T> source, DateTime from, DateTime to) where T : class =>
        source.Where(row => EF.Property<DateTime>(row, "PartitionTime") >= from
            && EF.Property<DateTime>(row, "PartitionTime") < to);

    /// <summary>单表读模型在时间索引上限定窗口。</summary>
    internal static IAsyncEnumerable<IQueryable<T>> ReadAsync<T>(IQueryable<T> source, DateTime from, DateTime to,
        CancellationToken token, int targetRows = TargetRows) where T : class =>
        ReadAsync((start, end) => Window(source, start, end), from, to, token, targetRows);

    /// <summary>在时间索引上定位下一边界，不重复计数二分；消费方直接枚举 EF，密集毫秒完整读取。</summary>
    internal static async IAsyncEnumerable<IQueryable<T>> ReadAsync<T>(Func<DateTime, DateTime, IQueryable<T>> queryWindow, DateTime from, DateTime to,
        [EnumeratorCancellation] CancellationToken token, int targetRows = TargetRows) where T : class {
        if (targetRows is < 1 or int.MaxValue || to <= from) throw new ArgumentException("报表读取窗口或目标行数无效。");
        var start = from;
        while (start < to) {
            token.ThrowIfCancellationRequested();
            var end = to;
            if (to.Ticks - start.Ticks > TimeSpan.TicksPerMillisecond) {
                var boundary = await BoundaryQuery(queryWindow(start, to), targetRows).ToArrayAsync(token);
                if (boundary.Length > 0) {
                    var ticks = boundary[0].Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond;
                    // 同毫秒超过预算时完整读取这一毫秒，不能按任意排序截断或遗漏事实。
                    if (ticks <= start.Ticks) ticks = (start.Ticks / TimeSpan.TicksPerMillisecond + 1) * TimeSpan.TicksPerMillisecond;
                    end = new DateTime(Math.Min(ticks, to.Ticks), start.Kind);
                }
            }
            yield return queryWindow(start, end);
            start = end;
        }
    }

    /// <summary>仅返回预算后的一个时间值，使用索引顺序跳过前缀；无需统计整个剩余区间。</summary>
    internal static IQueryable<DateTime> BoundaryQuery<T>(IQueryable<T> query, int targetRows) where T : class =>
        query.OrderBy(row => EF.Property<DateTime>(row, "PartitionTime"))
            .Select(row => EF.Property<DateTime>(row, "PartitionTime")).Skip(targetRows).Take(1);
}
