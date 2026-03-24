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
            StatusCode = readModel.StatusCode,
            IsSuccess = readModel.IsSuccess,
            HasException = readModel.HasException,
            StartedAt = readModel.StartedAt,
            EndedAt = readModel.EndedAt,
            DurationMs = readModel.DurationMs,
            CreatedAt = readModel.CreatedAt,
            RequestHeadersJson = readModel.RequestHeadersJson,
            ResponseHeadersJson = readModel.ResponseHeadersJson,
            RequestBody = readModel.RequestBody,
            ResponseBody = readModel.ResponseBody,
            ErrorMessage = readModel.ErrorMessage,
            ExceptionType = readModel.ExceptionType,
            ExceptionStackTrace = readModel.ExceptionStackTrace
        };
    }
}
