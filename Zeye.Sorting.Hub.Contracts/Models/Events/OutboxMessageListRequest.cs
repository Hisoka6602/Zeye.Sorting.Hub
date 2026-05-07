namespace Zeye.Sorting.Hub.Contracts.Models.Events;

/// <summary>
/// Outbox 消息分页查询请求合同。
/// </summary>
public sealed record OutboxMessageListRequest {
    /// <summary>
    /// 页码。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 状态过滤。
    /// </summary>
    public string? Status { get; init; }
}
