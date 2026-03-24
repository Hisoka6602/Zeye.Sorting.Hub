namespace Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;

/// <summary>
/// Web 请求审计日志详情响应合同。
/// </summary>
public sealed record WebRequestAuditLogDetailResponse {
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
    /// Span Id。
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// 操作名称。
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// 请求方法。
    /// </summary>
    public required string RequestMethod { get; init; }

    /// <summary>
    /// 请求协议。
    /// </summary>
    public required string RequestScheme { get; init; }

    /// <summary>
    /// 请求主机。
    /// </summary>
    public required string RequestHost { get; init; }

    /// <summary>
    /// 请求端口。
    /// </summary>
    public required int? RequestPort { get; init; }

    /// <summary>
    /// 请求路径。
    /// </summary>
    public required string RequestPath { get; init; }

    /// <summary>
    /// 路由模板。
    /// </summary>
    public required string RequestRouteTemplate { get; init; }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 是否存在异常。
    /// </summary>
    public required bool HasException { get; init; }

    /// <summary>
    /// 请求开始时间（本地时间语义）。
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// 请求结束时间（本地时间语义）。
    /// </summary>
    public required DateTime EndedAt { get; init; }

    /// <summary>
    /// 请求耗时（毫秒）。
    /// </summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// 创建时间（本地时间语义）。
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// 请求头 JSON。
    /// </summary>
    public required string RequestHeadersJson { get; init; }

    /// <summary>
    /// 响应头 JSON。
    /// </summary>
    public required string ResponseHeadersJson { get; init; }

    /// <summary>
    /// 请求体。
    /// </summary>
    public required string RequestBody { get; init; }

    /// <summary>
    /// 响应体。
    /// </summary>
    public required string ResponseBody { get; init; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// 异常类型。
    /// </summary>
    public required string ExceptionType { get; init; }

    /// <summary>
    /// 异常堆栈。
    /// </summary>
    public required string ExceptionStackTrace { get; init; }
}
