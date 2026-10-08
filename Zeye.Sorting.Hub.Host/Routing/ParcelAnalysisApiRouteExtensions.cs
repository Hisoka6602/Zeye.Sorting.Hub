using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Timeouts;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>包裹分析按有界日期查询，复用包裹读取权限。</summary>
public static class ParcelAnalysisApiRouteExtensions {
    /// <summary>登记数据库聚合和分页下钻的只读接口。</summary>
    public static IEndpointRouteBuilder MapParcelAnalysisApis(this IEndpointRouteBuilder routes) {
        routes.MapGet("/api/parcels/analysis", ReadAsync).WithTags("Parcels").WithName("GetParcelAnalysis")
            .WithSummary("查询包裹异常、DWS与接口耗时、格口流向分析")
            .WithDescription("按首次入库本地日期统计；耗时支持完成、DWS获取、格口决策、分拣执行和各业务接口调用。包裹阶段按票统计，接口按独立调用尝试统计，包含失败和重试；缺失或倒序时间不伪造耗时。各耗时类型独立缓存最长一分钟的统计快照，显式刷新读取当前类型的最新记录；汇总统计与分页明细分开，默认最多31天。")
            // 历史接口分析需兼容来源JSON元数据；独立有限预算不影响普通请求的15秒限制。
            .Produces<ParcelAnalysisResponse>().ProducesProblem(400).WithRequestTimeout(TimeSpan.FromSeconds(30));
        return routes;
    }

    /// <summary>拒绝UTC、带时间日期或非法区间后执行分析。</summary>
    private static async Task<IResult> ReadAsync([AsParameters] ParcelAnalysisQueryParameters query,
        [FromServices] IParcelAnalysisReadService service, CancellationToken cancellationToken) {
        if (query.FromDate?.Length != 10 || query.ToDate?.Length != 10
            || !LocalDateTimeParsing.TryParseLocalDateTime(query.FromDate, out var from)
            || !LocalDateTimeParsing.TryParseLocalDateTime(query.ToDate, out var to))
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "fromDate/toDate 必须是yyyy-MM-dd本地日期。");
        try {
            return Results.Ok(await service.ReadAsync(new ParcelAnalysisRequest {
                View = query.View ?? "exceptions", FromDate = from, ToDate = to, WorkstationName = query.WorkstationName,
                DurationType = query.DurationType ?? "completion", RefreshDurationSnapshot = query.RefreshDurationSnapshot ?? false,
                SourceInstanceId = query.SourceInstanceId, Issue = query.Issue ?? "exception", ExceptionType = query.ExceptionType,
                MinimumMilliseconds = query.MinimumMilliseconds, MaximumMilliseconds = query.MaximumMilliseconds,
                MismatchOnly = query.MismatchOnly ?? false, FallbackOnly = query.FallbackOnly ?? false, TargetChuteCode = query.TargetChuteCode,
                ActualChuteCode = query.ActualChuteCode, PageNumber = query.PageNumber ?? 1
            }, cancellationToken));
        }
        catch (ArgumentException exception) { return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
    }
}
