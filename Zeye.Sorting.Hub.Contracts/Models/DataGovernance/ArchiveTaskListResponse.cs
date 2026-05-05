namespace Zeye.Sorting.Hub.Contracts.Models.DataGovernance;

/// <summary>
/// 归档任务分页响应合同。
/// </summary>
public sealed record ArchiveTaskListResponse {
    /// <summary>
    /// 当前页数据。
    /// </summary>
    public IReadOnlyList<ArchiveTaskResponse> Items { get; init; } = Array.Empty<ArchiveTaskResponse>();

    /// <summary>
    /// 页码。
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 总条数。
    /// </summary>
    public long TotalCount { get; init; }
}
