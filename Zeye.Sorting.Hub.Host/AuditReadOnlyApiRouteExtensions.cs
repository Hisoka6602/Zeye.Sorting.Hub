using Microsoft.AspNetCore.Mvc;
using NLog;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;

namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// 审计日志只读 API 路由扩展。
/// </summary>
public static class AuditReadOnlyApiRouteExtensions {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册审计日志只读 API。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapAuditReadOnlyApis(this IEndpointRouteBuilder routeBuilder) {
        var group = routeBuilder
            .MapGroup("/api/audit/web-requests")
            .WithTags("Audit")
            .RequireAuthorization();

        group.MapGet(string.Empty, GetWebRequestAuditLogListAsync)
            .WithName("GetWebRequestAuditLogList")
            .WithSummary("分页查询 Web 请求审计日志列表")
            .WithDescription("按页码、分页大小与可选过滤条件（startedAt 区间、statusCode、isSuccess、traceId、correlationId、requestPathKeyword）查询审计日志摘要列表。时间参数必须为本地时间字符串，不允许 UTC 或时区偏移。仅限已授权访问。")
            .Produces<WebRequestAuditLogListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:long}", GetWebRequestAuditLogByIdAsync)
            .WithName("GetWebRequestAuditLogById")
            .WithSummary("按 Id 查询 Web 请求审计日志详情")
            .WithDescription("按审计日志主键查询热表字段与冷表详情字段；当资源不存在时返回 404。仅限已授权访问。")
            .Produces<WebRequestAuditLogDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routeBuilder;
    }

    /// <summary>
    /// 处理审计日志列表查询。
    /// </summary>
    /// <param name="query">查询参数。</param>
    /// <param name="queryService">分页查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetWebRequestAuditLogListAsync(
        [AsParameters] WebRequestAuditLogListQueryParameters query,
        GetWebRequestAuditLogPagedQueryService queryService,
        CancellationToken cancellationToken) {
        if (!LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.StartedAtStart, out var startedAtStart)
            || !LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.StartedAtEnd, out var startedAtEnd)) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "startedAtStart/startedAtEnd 必须是本地时间格式，且不允许包含 UTC 或时区偏移。");
        }

        try {
            var request = new WebRequestAuditLogListRequest {
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                StartedAtStart = startedAtStart,
                StartedAtEnd = startedAtEnd,
                StatusCode = query.StatusCode,
                IsSuccess = query.IsSuccess,
                TraceId = query.TraceId,
                CorrelationId = query.CorrelationId,
                RequestPathKeyword = query.RequestPathKeyword
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Web 请求审计日志列表查询参数校验失败。");
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理审计日志详情查询。
    /// </summary>
    /// <param name="id">审计日志 Id。</param>
    /// <param name="queryService">详情查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetWebRequestAuditLogByIdAsync(
        long id,
        GetWebRequestAuditLogByIdQueryService queryService,
        CancellationToken cancellationToken) {
        try {
            var response = await queryService.ExecuteAsync(id, cancellationToken);
            return response is null
                ? LocalDateTimeParsing.CreateNotFoundProblem("审计日志不存在", $"未找到 Id 为 {id} 的 Web 请求审计日志。")
                : Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Web 请求审计日志详情查询参数校验失败，Id={AuditLogId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }
}
