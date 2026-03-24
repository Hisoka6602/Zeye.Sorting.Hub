using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计后台写入队列项。
/// </summary>
public readonly record struct WebRequestAuditBackgroundEntry {
    /// <summary>
    /// 审计日志聚合对象。
    /// </summary>
    public required WebRequestAuditLog Log { get; init; }
    /// <summary>
    /// 追踪标识。
    /// </summary>
    public required string TraceId { get; init; }
    /// <summary>
    /// 关联标识。
    /// </summary>
    public required string CorrelationId { get; init; }
}
