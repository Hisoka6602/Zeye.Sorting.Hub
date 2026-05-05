namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 游标分页查询响应合同。
/// </summary>
public sealed record ParcelCursorListResponse {
    /// <summary>
    /// 当前页列表数据。
    /// </summary>
    public required IReadOnlyList<ParcelListItemResponse> Items { get; init; }

    /// <summary>
    /// 当前页大小。
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// 是否还有下一页。
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// 下一页游标；为空表示没有下一页。
    /// </summary>
    public string? NextCursor { get; init; }
}
