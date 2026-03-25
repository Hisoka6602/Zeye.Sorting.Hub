using Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

namespace Zeye.Sorting.Hub.Application.Services.AuditLogs;

/// <summary>
/// Web 请求审计日志合同映射器。
/// </summary>
internal static class WebRequestAuditLogContractMapper {
    /// <summary>
    /// 将摘要读模型映射为列表项合同。
    /// </summary>
    /// <param name="readModel">摘要读模型。</param>
    /// <returns>列表项合同。</returns>
    internal static WebRequestAuditLogListItemResponse ToListItem(WebRequestAuditLogSummaryReadModel readModel) {
        return new WebRequestAuditLogListItemResponse {
            Id = readModel.Id,
            TraceId = readModel.TraceId,
            CorrelationId = readModel.CorrelationId,
            RequestMethod = readModel.RequestMethod,
            RequestPath = readModel.RequestPath,
            StatusCode = readModel.StatusCode,
            IsSuccess = readModel.IsSuccess,
            StartedAt = readModel.StartedAt,
            DurationMs = readModel.DurationMs
        };
    }

    /// <summary>
    /// 将详情读模型映射为详情合同。
    /// </summary>
    /// <param name="readModel">详情读模型。</param>
    /// <returns>详情合同。</returns>
    internal static WebRequestAuditLogDetailResponse ToDetail(WebRequestAuditLogDetailReadModel readModel) {
        return new WebRequestAuditLogDetailResponse {
            WebRequestAuditLogId = readModel.WebRequestAuditLogId,
            Id = readModel.Id,
            TraceId = readModel.TraceId,
            CorrelationId = readModel.CorrelationId,
            SpanId = readModel.SpanId,
            OperationName = readModel.OperationName,
            RequestMethod = readModel.RequestMethod,
            RequestScheme = readModel.RequestScheme,
            RequestHost = readModel.RequestHost,
            RequestPort = readModel.RequestPort,
            RequestPath = readModel.RequestPath,
            RequestRouteTemplate = readModel.RequestRouteTemplate,
            UserId = readModel.UserId,
            UserName = readModel.UserName,
            IsAuthenticated = readModel.IsAuthenticated,
            TenantId = readModel.TenantId,
            RequestPayloadType = (int)readModel.RequestPayloadType,
            RequestSizeBytes = readModel.RequestSizeBytes,
            HasRequestBody = readModel.HasRequestBody,
            IsRequestBodyTruncated = readModel.IsRequestBodyTruncated,
            ResponsePayloadType = (int)readModel.ResponsePayloadType,
            ResponseSizeBytes = readModel.ResponseSizeBytes,
            HasResponseBody = readModel.HasResponseBody,
            IsResponseBodyTruncated = readModel.IsResponseBodyTruncated,
            StatusCode = readModel.StatusCode,
            IsSuccess = readModel.IsSuccess,
            HasException = readModel.HasException,
            AuditResourceType = (int)readModel.AuditResourceType,
            ResourceId = readModel.ResourceId,
            StartedAt = readModel.StartedAt,
            EndedAt = readModel.EndedAt,
            DurationMs = readModel.DurationMs,
            CreatedAt = readModel.CreatedAt,
            RequestUrl = readModel.RequestUrl,
            RequestQueryString = readModel.RequestQueryString,
            RequestHeadersJson = readModel.RequestHeadersJson,
            ResponseHeadersJson = readModel.ResponseHeadersJson,
            RequestContentType = readModel.RequestContentType,
            ResponseContentType = readModel.ResponseContentType,
            Accept = readModel.Accept,
            Referer = readModel.Referer,
            Origin = readModel.Origin,
            AuthorizationType = readModel.AuthorizationType,
            UserAgent = readModel.UserAgent,
            RequestBody = readModel.RequestBody,
            ResponseBody = readModel.ResponseBody,
            CurlCommand = readModel.CurlCommand,
            ErrorMessage = readModel.ErrorMessage,
            ExceptionType = readModel.ExceptionType,
            ErrorCode = readModel.ErrorCode,
            ExceptionStackTrace = readModel.ExceptionStackTrace,
            FileMetadataJson = readModel.FileMetadataJson,
            HasFileAccess = readModel.HasFileAccess,
            FileOperationType = (int)readModel.FileOperationType,
            FileCount = readModel.FileCount,
            FileTotalBytes = readModel.FileTotalBytes,
            ImageMetadataJson = readModel.ImageMetadataJson,
            HasImageAccess = readModel.HasImageAccess,
            ImageCount = readModel.ImageCount,
            DatabaseOperationSummary = readModel.DatabaseOperationSummary,
            HasDatabaseAccess = readModel.HasDatabaseAccess,
            DatabaseAccessCount = readModel.DatabaseAccessCount,
            DatabaseDurationMs = readModel.DatabaseDurationMs,
            ResourceCode = readModel.ResourceCode,
            ResourceName = readModel.ResourceName,
            ActionDurationMs = readModel.ActionDurationMs,
            MiddlewareDurationMs = readModel.MiddlewareDurationMs,
            Tags = readModel.Tags,
            ExtraPropertiesJson = readModel.ExtraPropertiesJson,
            Remark = readModel.Remark
        };
    }
}
