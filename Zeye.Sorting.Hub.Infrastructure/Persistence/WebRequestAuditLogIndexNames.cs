namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// WebRequestAuditLog 相关索引名称常量（供分表关键索引审计与测试复用）。
    /// </summary>
    public static class WebRequestAuditLogIndexNames {
        /// <summary>
        /// StartedAt 单列索引名。
        /// </summary>
        public const string StartedAt = "IX_WebRequestAuditLogs_StartedAt";

        /// <summary>
        /// StatusCode + StartedAt 复合索引名。
        /// </summary>
        public const string StatusCodeStartedAt = "IX_WebRequestAuditLogs_StatusCode_StartedAt";

        /// <summary>
        /// IsSuccess + StartedAt 复合索引名。
        /// </summary>
        public const string IsSuccessStartedAt = "IX_WebRequestAuditLogs_IsSuccess_StartedAt";

        /// <summary>
        /// OperationName + StartedAt 复合索引名。
        /// </summary>
        public const string OperationNameStartedAt = "IX_WebRequestAuditLogs_OperationName_StartedAt";

        /// <summary>
        /// RequestPath + StartedAt 复合索引名。
        /// </summary>
        public const string RequestPathStartedAt = "IX_WebRequestAuditLogs_RequestPath_StartedAt";

        /// <summary>
        /// TraceId 单列索引名。
        /// </summary>
        public const string TraceId = "IX_WebRequestAuditLogs_TraceId";

        /// <summary>
        /// CorrelationId 单列索引名。
        /// </summary>
        public const string CorrelationId = "IX_WebRequestAuditLogs_CorrelationId";

        /// <summary>
        /// UserId + StartedAt 复合索引名。
        /// </summary>
        public const string UserIdStartedAt = "IX_WebRequestAuditLogs_UserId_StartedAt";

        /// <summary>
        /// TenantId + StartedAt 复合索引名。
        /// </summary>
        public const string TenantIdStartedAt = "IX_WebRequestAuditLogs_TenantId_StartedAt";

        /// <summary>
        /// AuditResourceType + ResourceId + StartedAt 复合索引名。
        /// </summary>
        public const string AuditResourceTypeResourceIdStartedAt = "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt";
    }
}
