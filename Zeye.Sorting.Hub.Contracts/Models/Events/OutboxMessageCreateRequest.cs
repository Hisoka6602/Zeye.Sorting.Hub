namespace Zeye.Sorting.Hub.Contracts.Models.Events;

/// <summary>
/// Outbox 消息创建请求合同。
/// </summary>
public sealed record OutboxMessageCreateRequest {
    /// <summary>
    /// 事件类型。
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 事件载荷 JSON。
    /// </summary>
    public string PayloadJson { get; init; } = string.Empty;
}
