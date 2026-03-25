namespace Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;

/// <summary>
/// Web 请求审计日志详情响应合同。
/// </summary>
public sealed record WebRequestAuditLogDetailResponse {
    /// <summary>
    /// 审计日志详情主键兼外键。
    /// </summary>
    public required long WebRequestAuditLogId { get; init; }

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
    /// 用户 Id。
    /// </summary>
    public required long? UserId { get; init; }

    /// <summary>
    /// 用户名。
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// 是否已认证。
    /// </summary>
    public required bool IsAuthenticated { get; init; }

    /// <summary>
    /// 租户 Id。
    /// </summary>
    public required long? TenantId { get; init; }

    /// <summary>
    /// 请求载荷类型。
    /// </summary>
    public required int RequestPayloadType { get; init; }

    /// <summary>
    /// 请求体字节数。
    /// </summary>
    public required long RequestSizeBytes { get; init; }

    /// <summary>
    /// 是否存在请求体。
    /// </summary>
    public required bool HasRequestBody { get; init; }

    /// <summary>
    /// 请求体是否截断。
    /// </summary>
    public required bool IsRequestBodyTruncated { get; init; }

    /// <summary>
    /// 响应载荷类型。
    /// </summary>
    public required int ResponsePayloadType { get; init; }

    /// <summary>
    /// 响应体字节数。
    /// </summary>
    public required long ResponseSizeBytes { get; init; }

    /// <summary>
    /// 是否存在响应体。
    /// </summary>
    public required bool HasResponseBody { get; init; }

    /// <summary>
    /// 响应体是否截断。
    /// </summary>
    public required bool IsResponseBodyTruncated { get; init; }

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
    /// 审计资源类型。
    /// </summary>
    public required int AuditResourceType { get; init; }

    /// <summary>
    /// 资源 Id。
    /// </summary>
    public required string ResourceId { get; init; }

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
    /// 完整请求 URL。
    /// </summary>
    public required string RequestUrl { get; init; }

    /// <summary>
    /// 请求查询字符串。
    /// </summary>
    public required string RequestQueryString { get; init; }

    /// <summary>
    /// 请求头 JSON。
    /// </summary>
    public required string RequestHeadersJson { get; init; }

    /// <summary>
    /// 响应头 JSON。
    /// </summary>
    public required string ResponseHeadersJson { get; init; }

    /// <summary>
    /// 请求 ContentType。
    /// </summary>
    public required string RequestContentType { get; init; }

    /// <summary>
    /// 响应 ContentType。
    /// </summary>
    public required string ResponseContentType { get; init; }

    /// <summary>
    /// Accept 请求头。
    /// </summary>
    public required string Accept { get; init; }

    /// <summary>
    /// Referer 请求头。
    /// </summary>
    public required string Referer { get; init; }

    /// <summary>
    /// Origin 请求头。
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    /// 授权类型。
    /// </summary>
    public required string AuthorizationType { get; init; }

    /// <summary>
    /// User-Agent。
    /// </summary>
    public required string UserAgent { get; init; }

    /// <summary>
    /// 请求体。
    /// </summary>
    public required string RequestBody { get; init; }

    /// <summary>
    /// 响应体。
    /// </summary>
    public required string ResponseBody { get; init; }

    /// <summary>
    /// Curl 回放命令。
    /// </summary>
    public required string CurlCommand { get; init; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// 异常类型。
    /// </summary>
    public required string ExceptionType { get; init; }

    /// <summary>
    /// 错误码。
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// 异常堆栈。
    /// </summary>
    public required string ExceptionStackTrace { get; init; }

    /// <summary>
    /// 文件元数据 JSON。
    /// </summary>
    public required string FileMetadataJson { get; init; }

    /// <summary>
    /// 是否有文件访问。
    /// </summary>
    public required bool HasFileAccess { get; init; }

    /// <summary>
    /// 文件操作类型。
    /// </summary>
    public required int FileOperationType { get; init; }

    /// <summary>
    /// 文件数量。
    /// </summary>
    public required int FileCount { get; init; }

    /// <summary>
    /// 文件总字节数。
    /// </summary>
    public required long FileTotalBytes { get; init; }

    /// <summary>
    /// 图片元数据 JSON。
    /// </summary>
    public required string ImageMetadataJson { get; init; }

    /// <summary>
    /// 是否有图片访问。
    /// </summary>
    public required bool HasImageAccess { get; init; }

    /// <summary>
    /// 图片数量。
    /// </summary>
    public required int ImageCount { get; init; }

    /// <summary>
    /// 数据库操作摘要。
    /// </summary>
    public required string DatabaseOperationSummary { get; init; }

    /// <summary>
    /// 是否有数据库访问。
    /// </summary>
    public required bool HasDatabaseAccess { get; init; }

    /// <summary>
    /// 数据库访问次数。
    /// </summary>
    public required int DatabaseAccessCount { get; init; }

    /// <summary>
    /// 数据库耗时毫秒。
    /// </summary>
    public required long DatabaseDurationMs { get; init; }

    /// <summary>
    /// 资源编码。
    /// </summary>
    public required string ResourceCode { get; init; }

    /// <summary>
    /// 资源名称。
    /// </summary>
    public required string ResourceName { get; init; }

    /// <summary>
    /// Action 执行耗时毫秒。
    /// </summary>
    public required long ActionDurationMs { get; init; }

    /// <summary>
    /// 中间件耗时毫秒。
    /// </summary>
    public required long MiddlewareDurationMs { get; init; }

    /// <summary>
    /// 审计标签。
    /// </summary>
    public required string Tags { get; init; }

    /// <summary>
    /// 扩展属性 JSON。
    /// </summary>
    public required string ExtraPropertiesJson { get; init; }

    /// <summary>
    /// 备注。
    /// </summary>
    public required string Remark { get; init; }
}
