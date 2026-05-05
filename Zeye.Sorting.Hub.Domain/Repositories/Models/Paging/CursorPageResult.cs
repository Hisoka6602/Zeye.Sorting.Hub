namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

/// <summary>
/// 游标分页结果。
/// </summary>
/// <typeparam name="TItem">列表项类型。</typeparam>
public sealed record CursorPageResult<TItem> {
    /// <summary>
    /// 当前页数据。
    /// </summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// 当前页大小。
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// 是否还有下一页。
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// 下一页游标对应的最后扫码时间（本地时间语义）。
    /// </summary>
    public DateTime? NextScannedTimeLocal { get; init; }

    /// <summary>
    /// 下一页游标对应的最后记录 Id。
    /// </summary>
    public long? NextId { get; init; }
}
