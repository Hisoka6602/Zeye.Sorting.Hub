using Zeye.Sorting.Hub.Domain.Abstractions;
using Zeye.Sorting.Hub.Domain.Enums.Events;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Events;

/// <summary>
/// Inbox 消息聚合根。
/// 用于承载外部事件幂等消费记录、消费状态与过期清理边界。
/// </summary>
public sealed class InboxMessage : IEntity<long> {
    /// <summary>
    /// 来源系统最大长度。
    /// </summary>
    public const int MaxSourceSystemLength = 128;

    /// <summary>
    /// 消息标识最大长度。
    /// </summary>
    public const int MaxMessageIdLength = 128;

    /// <summary>
    /// 事件类型最大长度。
    /// </summary>
    public const int MaxEventTypeLength = 256;

    /// <summary>
    /// 失败消息最大长度。
    /// </summary>
    public const int MaxFailureMessageLength = 1024;

    /// <summary>
    /// 默认过期治理保留天数。
    /// </summary>
    public const int DefaultRetentionDays = 30;

    /// <summary>
    /// 主键。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 来源系统。
    /// </summary>
    public string SourceSystem { get; private set; } = string.Empty;

    /// <summary>
    /// 唯一消息标识。
    /// </summary>
    public string MessageId { get; private set; } = string.Empty;

    /// <summary>
    /// 事件类型。
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public InboxMessageStatus Status { get; private set; }

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
    /// 最近一次消费尝试时间（本地时间语义）。
    /// </summary>
    public DateTime? LastAttemptedAt { get; private set; }

    /// <summary>
    /// 消费完成时间（本地时间语义）。
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// 过期治理时间（本地时间语义）。
    /// 到达该时间后，可由后续治理链路按批清理该记录。
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// EF Core 反序列化构造函数。
    /// </summary>
    private InboxMessage() {
    }

    /// <summary>
    /// 创建待处理 Inbox 消息。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="eventType">事件类型。</param>
    /// <param name="expiresAt">过期治理时间。</param>
    /// <returns>Inbox 消息聚合。</returns>
    public static InboxMessage CreatePending(
        string sourceSystem,
        string messageId,
        string eventType,
        DateTime? expiresAt = null) {
        var now = DateTime.Now;
        var normalizedExpiresAt = NormalizeExpiresAt(expiresAt, now);
        return new InboxMessage {
            SourceSystem = NormalizeSourceSystem(sourceSystem),
            MessageId = NormalizeMessageId(messageId),
            EventType = NormalizeEventType(eventType),
            Status = InboxMessageStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = normalizedExpiresAt
        };
    }

    /// <summary>
    /// 规范化来源系统。
    /// </summary>
    /// <param name="sourceSystem">原始来源系统。</param>
    /// <returns>规范化后的来源系统。</returns>
    public static string NormalizeSourceSystem(string sourceSystem) {
        var normalized = NormalizeRequiredText(sourceSystem, nameof(sourceSystem), MaxSourceSystemLength);
        if (normalized.AsSpan().IndexOfAny('\r', '\n') >= 0) {
            throw new ArgumentException("来源系统不能包含换行符。", nameof(sourceSystem));
        }

        return normalized;
    }

    /// <summary>
    /// 规范化消息标识。
    /// </summary>
    /// <param name="messageId">原始消息标识。</param>
    /// <returns>规范化后的消息标识。</returns>
    public static string NormalizeMessageId(string messageId) {
        var normalized = NormalizeRequiredText(messageId, nameof(messageId), MaxMessageIdLength);
        if (normalized.AsSpan().IndexOfAny('\r', '\n') >= 0) {
            throw new ArgumentException("消息标识不能包含换行符。", nameof(messageId));
        }

        return normalized;
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
    /// 标记进入处理中。
    /// </summary>
    public void MarkProcessing() {
        if (Status is not InboxMessageStatus.Pending and not InboxMessageStatus.Failed) {
            throw new InvalidOperationException("当前状态不允许进入处理中。");
        }

        if (IsExpired(DateTime.Now)) {
            throw new InvalidOperationException("Inbox 消息已过期，不允许继续消费。");
        }

        var now = DateTime.Now;
        Status = InboxMessageStatus.Processing;
        FailureMessage = string.Empty;
        LastAttemptedAt = now;
        ProcessedAt = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// 标记消费成功。
    /// </summary>
    public void MarkSucceeded() {
        if (Status != InboxMessageStatus.Processing) {
            throw new InvalidOperationException("仅处理中消息允许标记成功。");
        }

        var now = DateTime.Now;
        Status = InboxMessageStatus.Succeeded;
        FailureMessage = string.Empty;
        ProcessedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 标记消费失败。
    /// </summary>
    /// <param name="failureMessage">失败消息。</param>
    public void MarkFailed(string failureMessage) {
        if (Status != InboxMessageStatus.Processing) {
            throw new InvalidOperationException("仅处理中消息允许标记失败。");
        }

        var now = DateTime.Now;
        Status = InboxMessageStatus.Failed;
        RetryCount++;
        FailureMessage = NormalizeFailureMessage(failureMessage);
        ProcessedAt = null;
        LastAttemptedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 判断当前记录是否允许重试。
    /// </summary>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <returns>允许重试返回 true。</returns>
    public bool CanRetry(int maxRetryCount) {
        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "最大重试次数必须大于 0。");
        }

        return Status == InboxMessageStatus.Failed && RetryCount < maxRetryCount && !IsExpired(DateTime.Now);
    }

    /// <summary>
    /// 判断当前记录是否已过期。
    /// </summary>
    /// <param name="now">当前时间。</param>
    /// <returns>已过期返回 true。</returns>
    public bool IsExpired(DateTime now) {
        return now >= ExpiresAt;
    }

    /// <summary>
    /// 规范化过期治理时间。
    /// </summary>
    /// <param name="expiresAt">原始过期治理时间。</param>
    /// <param name="now">当前时间。</param>
    /// <returns>规范化后的过期治理时间。</returns>
    private static DateTime NormalizeExpiresAt(DateTime? expiresAt, DateTime now) {
        var resolvedExpiresAt = expiresAt ?? now.AddDays(DefaultRetentionDays);
        if (resolvedExpiresAt < now) {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "过期治理时间不能早于当前时间。");
        }

        return resolvedExpiresAt.Kind switch {
            DateTimeKind.Local => resolvedExpiresAt,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(resolvedExpiresAt, DateTimeKind.Local),
            _ => throw new ArgumentException("过期治理时间必须使用本地时间语义。", nameof(expiresAt))
        };
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
    /// 规范化失败消息。
    /// </summary>
    /// <param name="failureMessage">原始失败消息。</param>
    /// <returns>规范化后的失败消息。</returns>
    private static string NormalizeFailureMessage(string failureMessage) {
        var normalized = string.IsNullOrWhiteSpace(failureMessage) ? "Inbox 消息消费失败。" : failureMessage.Trim();
        return normalized.Length <= MaxFailureMessageLength
            ? normalized
            : normalized[..MaxFailureMessageLength];
    }
}
