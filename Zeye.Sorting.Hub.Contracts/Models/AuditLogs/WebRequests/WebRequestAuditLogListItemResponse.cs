namespace Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;

/// <summary>
/// Web 请求审计日志列表项响应合同。
/// </summary>
public sealed record WebRequestAuditLogListItemResponse {
    /// <summary>
    /// 审计日志主键。
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// 调用链追踪 Id。
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// 业务关联 Id。
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// 请求方法。
    /// </summary>
    public required string RequestMethod { get; init; }

    /// <summary>
    /// 请求路径。
    /// </summary>
    public required string RequestPath { get; init; }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 请求开始时间（本地时间语义）。
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// 请求耗时（毫秒）。
    /// </summary>
    public required long DurationMs { get; init; }
}
