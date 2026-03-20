namespace Zeye.Sorting.Hub.Domain.Repositories.Models;

/// <summary>
/// Parcel 分页请求参数。
/// </summary>
public sealed record ParcelPageRequest {
    /// <summary>
    /// 默认页码（用于无效页码兜底）。
    /// </summary>
    private const int DefaultPageNumber = 1;

    /// <summary>
    /// 默认分页大小（用于无效分页大小兜底）。
    /// </summary>
    private const int DefaultPageSize = 20;

    /// <summary>
    /// 最大分页大小（用于限制单次查询开销）。
    /// </summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// 页码（从 1 开始）。
    /// </summary>
    public int PageNumber { get; init; } = DefaultPageNumber;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// 将页码归一化到有效区间。
    /// </summary>
    public int NormalizePageNumber() {
        return PageNumber > 0 ? PageNumber : DefaultPageNumber;
    }

    /// <summary>
    /// 将页大小归一化到有效区间。
    /// </summary>
    public int NormalizePageSize() {
        var size = PageSize > 0 ? PageSize : DefaultPageSize;
        return size > MaxPageSize ? MaxPageSize : size;
    }
}
