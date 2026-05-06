namespace Zeye.Sorting.Hub.Contracts.Models.Events;

/// <summary>
/// Outbox 消息响应合同。
/// </summary>
public sealed record OutboxMessageResponse {
    /// <summary>
    /// 消息主键。
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 事件类型。
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 事件载荷 JSON。
    /// </summary>
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 已触发重试次数。
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 最近一次失败消息。
    /// </summary>
    public string FailureMessage { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间（本地时间语义）。
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 最近更新时间（本地时间语义）。
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 最近一次派发尝试时间（本地时间语义）。
    /// </summary>
    public DateTime? LastAttemptedAt { get; init; }

    /// <summary>
    /// 完成时间（本地时间语义）。
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}
