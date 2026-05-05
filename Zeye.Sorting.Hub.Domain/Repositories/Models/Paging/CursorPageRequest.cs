namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

/// <summary>
/// 游标分页请求参数。
/// </summary>
public sealed record CursorPageRequest {
    /// <summary>
    /// 默认页大小。
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// 最大页大小。
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// 上一页最后一条记录的扫码时间（本地时间语义）。
    /// </summary>
    public DateTime? LastScannedTimeLocal { get; init; }

    /// <summary>
    /// 上一页最后一条记录的主键 Id。
    /// </summary>
    public long? LastId { get; init; }

    /// <summary>
    /// 将页大小归一化到有效区间。
    /// </summary>
    /// <returns>归一化后的页大小。</returns>
    public int NormalizePageSize() {
        var normalizedPageSize = PageSize > 0 ? PageSize : DefaultPageSize;
        return normalizedPageSize > MaxPageSize ? MaxPageSize : normalizedPageSize;
    }
}
