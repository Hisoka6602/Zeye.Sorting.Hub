using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>
/// Parcel 游标分页查询扩展。
/// </summary>
internal static class ParcelCursorQueryExtensions {
    /// <summary>
    /// 按游标条件限制查询范围。
    /// </summary>
    /// <param name="query">基础查询。</param>
    /// <param name="pageRequest">游标分页请求。</param>
    /// <returns>追加游标条件后的查询。</returns>
    public static IQueryable<Parcel> ApplyCursorCondition(this IQueryable<Parcel> query, CursorPageRequest pageRequest) {
        if (!pageRequest.LastScannedTimeLocal.HasValue || !pageRequest.LastId.HasValue) {
            return query;
        }

        var lastScannedTimeLocal = pageRequest.LastScannedTimeLocal.Value;
        var lastId = pageRequest.LastId.Value;
        return query.Where(x => x.ScannedTime < lastScannedTimeLocal
            || (x.ScannedTime == lastScannedTimeLocal && x.Id < lastId));
    }
}
