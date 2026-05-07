using Zeye.Sorting.Hub.Domain.Abstractions;
using Zeye.Sorting.Hub.Domain.Enums.Events;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Events;

/// <summary>
/// Outbox 消息聚合根。
/// 用于承载业务事件的持久化记录、状态推进与失败隔离信息。
/// </summary>
public sealed class OutboxMessage : IEntity<long> {
    /// <summary>
    /// 事件类型最大长度。
    /// </summary>
    public const int MaxEventTypeLength = 256;

    /// <summary>
    /// 失败消息最大长度。
    /// </summary>
    public const int MaxFailureMessageLength = 1024;

    /// <summary>
    /// 主键。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 事件类型。
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// 事件载荷 JSON。
    /// </summary>
    public string PayloadJson { get; private set; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public OutboxMessageStatus Status { get; private set; }

    /// <summary>
    /// 已触发重试次数。
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// 最近一次失败消息。
    /// </summary>
    public string FailureMessage { get; private set; } = string.Empty;

    /// <summary>
    /// 创建时间（本地时间语义）。
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 最近更新时间（本地时间语义）。
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// 最近一次派发尝试时间（本地时间语义）。
    /// </summary>
    public DateTime? LastAttemptedAt { get; private set; }

    /// <summary>
    /// 终态完成时间（本地时间语义）。
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// EF Core 反序列化构造函数。
    /// </summary>
    private OutboxMessage() {
    }

    /// <summary>
    /// 创建待派发 Outbox 消息。
    /// </summary>
    /// <param name="eventType">事件类型。</param>
    /// <param name="payloadJson">事件载荷 JSON。</param>
    /// <returns>Outbox 消息聚合。</returns>
    public static OutboxMessage CreatePending(string eventType, string payloadJson) {
        var now = DateTime.Now;
        return new OutboxMessage {
            EventType = NormalizeEventType(eventType),
            PayloadJson = NormalizePayloadJson(payloadJson),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 规范化事件类型。
    /// </summary>
    /// <param name="eventType">原始事件类型。</param>
    /// <returns>规范化后的事件类型。</returns>
    public static string NormalizeEventType(string eventType) {
        var normalized = NormalizeRequiredText(eventType, nameof(eventType), MaxEventTypeLength);
        if (normalized.AsSpan().IndexOfAny('\r', '\n') >= 0) {
            throw new ArgumentException("事件类型不能包含换行符。", nameof(eventType));
        }

        return normalized;
    }

    /// <summary>
    /// 标记消息进入处理中。
    /// </summary>
    public void MarkProcessing() {
        if (Status is not OutboxMessageStatus.Pending and not OutboxMessageStatus.Failed) {
            throw new InvalidOperationException("当前状态不允许进入处理中。");
        }

        var now = DateTime.Now;
        Status = OutboxMessageStatus.Processing;
        FailureMessage = string.Empty;
        LastAttemptedAt = now;
        CompletedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 回收处理超时的消息，并在达到上限时进入死信。
    /// 处理超时视为一次失败尝试，因此会先递增 <see cref="RetryCount"/>。
    /// </summary>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <returns>若返回 true 表示已重新进入处理中；返回 false 表示已转入死信。</returns>
    public bool RecoverTimedOutProcessing(int maxRetryCount) {
        if (Status != OutboxMessageStatus.Processing) {
            throw new InvalidOperationException("仅处理中消息允许执行超时回收。");
        }

        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "最大重试次数必须大于 0。");
        }

        var now = DateTime.Now;
        RetryCount++;
        LastAttemptedAt = now;
        UpdatedAt = now;
        if (RetryCount >= maxRetryCount) {
            Status = OutboxMessageStatus.DeadLettered;
            FailureMessage = "Outbox 消息处理超时，已达到最大重试次数并进入死信。";
            CompletedAt = now;
            return false;
        }

        FailureMessage = "Outbox 消息处理超时，已自动回收重试。";
        CompletedAt = null;
        return true;
    }

    /// <summary>
    /// 标记消息派发成功。
    /// </summary>
    public void MarkDispatchSucceeded() {
        if (Status != OutboxMessageStatus.Processing) {
            throw new InvalidOperationException("仅处理中消息允许标记成功。");
        }

        var now = DateTime.Now;
        Status = OutboxMessageStatus.Succeeded;
        FailureMessage = string.Empty;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 标记消息派发失败，并在达到上限时进入死信。
    /// 每次失败都会先递增 <see cref="RetryCount"/>，递增后的次数达到 <paramref name="maxRetryCount"/> 时转入死信。
    /// </summary>
    /// <param name="failureMessage">失败原因。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    public void MarkDispatchFailed(string failureMessage, int maxRetryCount) {
        if (Status != OutboxMessageStatus.Processing) {
            throw new InvalidOperationException("仅处理中消息允许标记失败。");
        }

        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "最大重试次数必须大于 0。");
        }

        var now = DateTime.Now;
        RetryCount++;
        FailureMessage = NormalizeFailureMessage(failureMessage);
        if (RetryCount >= maxRetryCount) {
            Status = OutboxMessageStatus.DeadLettered;
            CompletedAt = now;
        }
        else {
            Status = OutboxMessageStatus.Failed;
            CompletedAt = null;
        }

        UpdatedAt = now;
        LastAttemptedAt = now;
    }

    /// <summary>
    /// 规范化必填文本。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <returns>规范化后的文本。</returns>
    private static string NormalizeRequiredText(string value, string paramName, int maxLength) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("值不能为空。", paramName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength) {
            throw new ArgumentException($"长度不能超过 {maxLength} 个字符。", paramName);
        }

        return normalized;
    }

    /// <summary>
    /// 规范化 JSON 载荷。
    /// </summary>
    /// <param name="payloadJson">原始 JSON。</param>
    /// <returns>规范化后的 JSON 字符串。</returns>
    private static string NormalizePayloadJson(string payloadJson) {
        return NormalizeRequiredText(payloadJson, nameof(payloadJson), int.MaxValue);
    }

    /// <summary>
    /// 规范化失败消息。
    /// </summary>
    /// <param name="failureMessage">原始失败消息。</param>
    /// <returns>规范化后的失败消息。</returns>
    private static string NormalizeFailureMessage(string failureMessage) {
        var normalized = string.IsNullOrWhiteSpace(failureMessage) ? "Outbox 消息派发失败。" : failureMessage.Trim();
        return normalized.Length <= MaxFailureMessageLength
            ? normalized
            : normalized[..MaxFailureMessageLength];
    }
}
