using Zeye.Sorting.Hub.Domain.Abstractions;
using Zeye.Sorting.Hub.Domain.Enums.Idempotency;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;

/// <summary>
/// 幂等记录聚合根。
/// </summary>
public sealed class IdempotencyRecord : IEntity<long> {
    /// <summary>
    /// 来源系统最大长度。
    /// </summary>
    public const int MaxSourceSystemLength = 128;

    /// <summary>
    /// 操作名称最大长度。
    /// </summary>
    public const int MaxOperationNameLength = 128;

    /// <summary>
    /// 业务键最大长度。
    /// </summary>
    public const int MaxBusinessKeyLength = 256;

    /// <summary>
    /// 载荷哈希长度（SHA256 十六进制）。
    /// </summary>
    public const int PayloadHashLength = 64;

    /// <summary>
    /// 失败消息最大长度。
    /// </summary>
    public const int MaxFailureMessageLength = 1024;

    /// <summary>
    /// 主键。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 来源系统。
    /// </summary>
    public string SourceSystem { get; private set; } = string.Empty;

    /// <summary>
    /// 操作名称。
    /// </summary>
    public string OperationName { get; private set; } = string.Empty;

    /// <summary>
    /// 业务键。
    /// </summary>
    public string BusinessKey { get; private set; } = string.Empty;

    /// <summary>
    /// 载荷哈希。
    /// </summary>
    public string PayloadHash { get; private set; } = string.Empty;

    /// <summary>
    /// 当前状态。
    /// </summary>
    public IdempotencyRecordStatus Status { get; private set; }

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
    /// 完成时间（本地时间语义）。
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// EF Core 反序列化构造函数。
    /// </summary>
    private IdempotencyRecord() {
    }

    /// <summary>
    /// 创建待处理幂等记录。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="payloadHash">载荷哈希。</param>
    /// <returns>幂等记录聚合。</returns>
    public static IdempotencyRecord CreatePending(
        string sourceSystem,
        string operationName,
        string businessKey,
        string payloadHash) {
        var now = DateTime.Now;
        return new IdempotencyRecord {
            SourceSystem = NormalizeRequiredText(sourceSystem, nameof(sourceSystem), MaxSourceSystemLength),
            OperationName = NormalizeRequiredText(operationName, nameof(operationName), MaxOperationNameLength),
            BusinessKey = NormalizeRequiredText(businessKey, nameof(businessKey), MaxBusinessKeyLength),
            PayloadHash = NormalizePayloadHash(payloadHash),
            Status = IdempotencyRecordStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 标记执行完成。
    /// </summary>
    public void MarkCompleted() {
        var now = DateTime.Now;
        Status = IdempotencyRecordStatus.Completed;
        FailureMessage = string.Empty;
        CompletedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// 标记执行失败。
    /// </summary>
    /// <param name="failureMessage">失败消息。</param>
    public void MarkFailed(string failureMessage) {
        var now = DateTime.Now;
        Status = IdempotencyRecordStatus.Failed;
        FailureMessage = NormalizeFailureMessage(failureMessage);
        UpdatedAt = now;
    }

    /// <summary>
    /// 标记被拒绝。
    /// </summary>
    /// <param name="failureMessage">拒绝原因。</param>
    public void MarkRejected(string failureMessage) {
        var now = DateTime.Now;
        Status = IdempotencyRecordStatus.Rejected;
        FailureMessage = NormalizeFailureMessage(failureMessage);
        UpdatedAt = now;
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
    /// 规范化载荷哈希。
    /// </summary>
    /// <param name="payloadHash">原始载荷哈希。</param>
    /// <returns>规范化后的载荷哈希。</returns>
    private static string NormalizePayloadHash(string payloadHash) {
        var normalized = NormalizeRequiredText(payloadHash, nameof(payloadHash), PayloadHashLength).ToUpperInvariant();
        if (normalized.Length != PayloadHashLength) {
            throw new ArgumentException($"载荷哈希长度必须为 {PayloadHashLength}。", nameof(payloadHash));
        }

        return normalized;
    }

    /// <summary>
    /// 规范化失败消息。
    /// </summary>
    /// <param name="failureMessage">原始失败消息。</param>
    /// <returns>规范化后的失败消息。</returns>
    private static string NormalizeFailureMessage(string failureMessage) {
        var normalized = string.IsNullOrWhiteSpace(failureMessage) ? "幂等请求执行失败。" : failureMessage.Trim();
        return normalized.Length <= MaxFailureMessageLength
            ? normalized
            : normalized[..MaxFailureMessageLength];
    }
}
