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
            EventType = NormalizeRequiredText(eventType, nameof(eventType), MaxEventTypeLength),
            PayloadJson = NormalizePayloadJson(payloadJson),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
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
