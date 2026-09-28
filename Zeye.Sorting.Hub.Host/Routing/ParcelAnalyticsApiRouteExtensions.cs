using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>包裹运营分析只读路由。</summary>
public static class ParcelAnalyticsApiRouteExtensions {
    /// <summary>查询参数异常审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>注册按本地日期聚合的真实报表接口。</summary>
    public static IEndpointRouteBuilder MapParcelAnalyticsApis(this IEndpointRouteBuilder routes) {
        routes.MapGet("/api/parcels/analytics", GetAsync)
            .WithTags("Parcels")
            .WithName("GetParcelAnalytics")
            .WithSummary("查询来源包裹运营报表")
            .WithDescription("fromDate/toDate 为包含两端的本地日期 yyyy-MM-dd。包裹指标按首次入库日和当前快照统计；处理事实按发生日单独统计，不共享分母。范围受 MaxReportTimeRangeDays 限制，默认最多31天。")
            .Produces<ParcelAnalyticsResponse>()
            .ProducesProblem(400);
        return routes;
    }

    /// <summary>校验严格本地日期后执行有界数据库聚合。</summary>
    private static async Task<IResult> GetAsync(string? fromDate, string? toDate, IParcelAnalyticsReadService service, CancellationToken cancellationToken) {
        if (fromDate?.Length != 10 || toDate?.Length != 10
            || !LocalDateTimeParsing.TryParseLocalDateTime(fromDate, out var from)
            || !LocalDateTimeParsing.TryParseLocalDateTime(toDate, out var to)) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "fromDate/toDate 必须是 yyyy-MM-dd 本地日期。");
        }
        try {
            return Results.Ok(await service.GetAsync(from, to, cancellationToken));
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "包裹报表查询参数无效，FromDate={FromDate}, ToDate={ToDate}", fromDate, toDate);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }
}
