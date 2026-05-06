namespace Zeye.Sorting.Hub.Contracts.Models.Events;

/// <summary>
/// Outbox 消息分页响应合同。
/// </summary>
public sealed record OutboxMessageListResponse {
    /// <summary>
    /// 当前页数据。
    /// </summary>
    public IReadOnlyList<OutboxMessageResponse> Items { get; init; } = Array.Empty<OutboxMessageResponse>();

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
