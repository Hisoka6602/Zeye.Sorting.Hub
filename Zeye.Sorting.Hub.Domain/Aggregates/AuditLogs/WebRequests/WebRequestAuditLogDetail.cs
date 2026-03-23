using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;

namespace Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests {

    /// <summary>
    /// Web 请求审计日志详情实体（冷数据详情表）。
    /// 说明：承载大文本与低频读取字段，避免主表过宽影响高频写入性能。
    /// </summary>
    public sealed class WebRequestAuditLogDetail {
        /// <summary>
        /// 主键兼外键（关联主表 Id）。
        /// </summary>
        public long WebRequestAuditLogId { get; set; }

        /// <summary>
        /// 分表路由时间（与主表 StartedAt 同步，本地时间语义）。
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// 完整请求 URL。
        /// </summary>
        public string RequestUrl { get; set; } = string.Empty;

        /// <summary>
        /// 请求查询字符串。
        /// </summary>
        public string RequestQueryString { get; set; } = string.Empty;

        /// <summary>
        /// 请求头 JSON。
        /// </summary>
        public string RequestHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// 响应头 JSON。
        /// </summary>
        public string ResponseHeadersJson { get; set; } = string.Empty;

        /// <summary>
        /// 请求 ContentType。
        /// </summary>
        public string RequestContentType { get; set; } = string.Empty;

        /// <summary>
        /// 响应 ContentType。
        /// </summary>
        public string ResponseContentType { get; set; } = string.Empty;

        /// <summary>
        /// Accept 请求头。
        /// </summary>
        public string Accept { get; set; } = string.Empty;

        /// <summary>
        /// Referer 请求头。
        /// </summary>
        public string Referer { get; set; } = string.Empty;

        /// <summary>
        /// Origin 请求头。
        /// </summary>
        public string Origin { get; set; } = string.Empty;

        /// <summary>
        /// 授权类型。
        /// </summary>
        public string AuthorizationType { get; set; } = string.Empty;

        /// <summary>
        /// User-Agent。
        /// </summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>
        /// 请求体。
        /// </summary>
        public string RequestBody { get; set; } = string.Empty;

        /// <summary>
        /// 响应体。
        /// </summary>
        public string ResponseBody { get; set; } = string.Empty;

        /// <summary>
        /// Curl 回放命令。
        /// </summary>
        public string CurlCommand { get; set; } = string.Empty;

        /// <summary>
        /// 错误消息。
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 异常类型。
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// 错误码。
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// 异常堆栈。
        /// </summary>
        public string ExceptionStackTrace { get; set; } = string.Empty;

        /// <summary>
        /// 文件元数据 JSON。
        /// </summary>
        public string FileMetadataJson { get; set; } = string.Empty;

        /// <summary>
        /// 是否有文件访问。
        /// </summary>
        public bool HasFileAccess { get; set; } = false;

        /// <summary>
        /// 文件操作类型。
        /// </summary>
        public FileOperationType FileOperationType { get; set; } = FileOperationType.None;

        /// <summary>
        /// 文件数量。
        /// </summary>
        public int FileCount { get; set; } = 0;

        /// <summary>
        /// 文件总字节数。
        /// </summary>
        public long FileTotalBytes { get; set; } = 0;

        /// <summary>
        /// 图片元数据 JSON。
        /// </summary>
        public string ImageMetadataJson { get; set; } = string.Empty;

        /// <summary>
        /// 是否有图片访问。
        /// </summary>
        public bool HasImageAccess { get; set; } = false;

        /// <summary>
        /// 图片数量。
        /// </summary>
        public int ImageCount { get; set; } = 0;

        /// <summary>
        /// 数据库操作摘要。
        /// </summary>
        public string DatabaseOperationSummary { get; set; } = string.Empty;

        /// <summary>
        /// 是否有数据库访问。
        /// </summary>
        public bool HasDatabaseAccess { get; set; } = false;

        /// <summary>
        /// 数据库访问次数。
        /// </summary>
        public int DatabaseAccessCount { get; set; } = 0;

        /// <summary>
        /// 数据库耗时毫秒。
        /// </summary>
        public long DatabaseDurationMs { get; set; } = 0;

        /// <summary>
        /// 资源编码。
        /// </summary>
        public string ResourceCode { get; set; } = string.Empty;

        /// <summary>
        /// 资源名称。
        /// </summary>
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Action 执行耗时毫秒。
        /// </summary>
        public long ActionDurationMs { get; set; } = 0;

        /// <summary>
        /// 中间件耗时毫秒。
        /// </summary>
        public long MiddlewareDurationMs { get; set; } = 0;

        /// <summary>
        /// 审计标签。
        /// </summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// 扩展属性 JSON。
        /// </summary>
        public string ExtraPropertiesJson { get; set; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 主表导航。
        /// </summary>
        public WebRequestAuditLog? WebRequestAuditLog { get; set; }
    }
}
