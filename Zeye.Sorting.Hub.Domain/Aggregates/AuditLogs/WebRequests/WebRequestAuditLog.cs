using Zeye.Sorting.Hub.Domain.Abstractions;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;

namespace Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests {

    /// <summary>
    /// Web 请求审计日志聚合根（热数据主表）。
    /// 说明：写多读少场景下将高频筛选字段保留在热表，降低行宽与热索引负担。
    /// </summary>
    public sealed class WebRequestAuditLog : IEntity<long> {
        /// <summary>
        /// 主键。
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 调用链追踪 Id。
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        /// <summary>
        /// 业务关联 Id。
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Span Id。
        /// </summary>
        public string SpanId { get; set; } = string.Empty;

        /// <summary>
        /// 操作名称。
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// 请求方法。
        /// </summary>
        public string RequestMethod { get; set; } = string.Empty;

        /// <summary>
        /// 请求协议。
        /// </summary>
        public string RequestScheme { get; set; } = string.Empty;

        /// <summary>
        /// 请求主机。
        /// </summary>
        public string RequestHost { get; set; } = string.Empty;

        /// <summary>
        /// 请求端口。
        /// </summary>
        public int? RequestPort { get; set; }

        /// <summary>
        /// 请求路径。
        /// </summary>
        public string RequestPath { get; set; } = string.Empty;

        /// <summary>
        /// 路由模板。
        /// </summary>
        public string RequestRouteTemplate { get; set; } = string.Empty;

        /// <summary>
        /// 用户 Id。
        /// </summary>
        public long? UserId { get; set; }

        /// <summary>
        /// 用户名。
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 是否已认证。
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// 租户 Id。
        /// </summary>
        public long? TenantId { get; set; }

        /// <summary>
        /// 请求载荷类型。
        /// </summary>
        public WebRequestPayloadType RequestPayloadType { get; set; }

        /// <summary>
        /// 请求体字节数。
        /// </summary>
        public long RequestSizeBytes { get; set; }

        /// <summary>
        /// 是否存在请求体。
        /// </summary>
        public bool HasRequestBody { get; set; }

        /// <summary>
        /// 请求体是否截断。
        /// </summary>
        public bool IsRequestBodyTruncated { get; set; }

        /// <summary>
        /// 响应载荷类型。
        /// </summary>
        public WebResponsePayloadType ResponsePayloadType { get; set; }

        /// <summary>
        /// 响应体字节数。
        /// </summary>
        public long ResponseSizeBytes { get; set; }

        /// <summary>
        /// 是否存在响应体。
        /// </summary>
        public bool HasResponseBody { get; set; }

        /// <summary>
        /// 响应体是否截断。
        /// </summary>
        public bool IsResponseBodyTruncated { get; set; }

        /// <summary>
        /// HTTP 状态码。
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 是否成功。
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 是否存在异常。
        /// </summary>
        public bool HasException { get; set; }

        /// <summary>
        /// 审计资源类型。
        /// </summary>
        public AuditResourceType AuditResourceType { get; set; }

        /// <summary>
        /// 资源 Id。
        /// </summary>
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// 请求开始时间（本地时间语义）。
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// 请求结束时间（本地时间语义）。
        /// </summary>
        public DateTime EndedAt { get; set; }

        /// <summary>
        /// 总耗时毫秒。
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// 创建时间（本地时间语义）。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 冷数据详情实体。
        /// </summary>
        public WebRequestAuditLogDetail? Detail { get; set; }
    }
}
