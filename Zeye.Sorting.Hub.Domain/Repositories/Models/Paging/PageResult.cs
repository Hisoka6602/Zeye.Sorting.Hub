namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

/// <summary>
/// 通用分页结果。
/// </summary>
public sealed record PageResult<TItem> {
    /// <summary>
    /// 当前页数据。
    /// </summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// 当前页码。
    /// </summary>
    public required int PageNumber { get; init; }

    /// <summary>
    /// 当前页大小。
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// 总记录数。
    /// </summary>
    public required long TotalCount { get; init; }
}
