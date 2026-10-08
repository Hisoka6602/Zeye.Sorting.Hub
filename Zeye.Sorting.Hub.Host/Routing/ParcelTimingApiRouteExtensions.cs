using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>只读包裹时序API，沿用parcels.read权限边界。</summary>
public static class ParcelTimingApiRouteExtensions {
    /// <summary>登记时序候选和有界邻近只读端点。</summary>
    public static IEndpointRouteBuilder MapParcelTimingApis(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/parcels/timing").WithTags("Parcels");
        group.MapGet("/candidates", async (string? query, string? searchBy, int? pageNumber, [FromServices] IParcelTimingReadService service, CancellationToken token) => {
            try { return Results.Ok(await service.SearchAsync(query ?? string.Empty, searchBy ?? "auto", pageNumber ?? 1, token)); }
            catch (ArgumentException exception) { return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
        }).WithName("GetParcelTimingCandidates").WithSummary("按包裹Id或完整条码精确查询时序候选")
            .WithDescription("auto同时匹配有效Id与完整条码；重复条码需选择包裹。查询覆盖所有已登记分表和历史基础表，每页20票。")
            .Produces<ParcelTimingCandidatesResponse>().ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/compare", async (string? ids, [FromServices] IParcelTimingReadService service, CancellationToken token) => {
            var values = (ids ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
            if (values.Length is < 1 or > 8 || values.Any(value => !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0))
                return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "ids必须为1至8个逗号分隔的有效包裹 ID。");
            try { return Results.Ok(await service.CompareAsync(values.Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray(), token)); }
            catch (ArgumentException exception) { return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
        }).WithName("GetParcelComparison").WithSummary("读取用户选定的多票包裹量测与动作时序")
            .WithDescription("按输入顺序返回最多8票，64位编号为字符串；缺失包裹显式列出。量测使用当前快照，时序保留真实端点，不自动读取邻票。")
            .Produces<ParcelComparisonResponse>().ProducesProblem(StatusCodes.Status400BadRequest);
        group.MapGet("/{id:long}", async (long id, [FromServices] IParcelTimingReadService service, CancellationToken token) => {
            try {
                var response = await service.ReadAsync(id, token);
                return response is null ? LocalDateTimeParsing.CreateParcelMissingProblem(id) : Results.Ok(response);
            }
            catch (ArgumentException exception) { return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
        }).WithName("GetParcelTiming").WithSummary("读取锚点包裹及前后各5票的动作时序")
            .WithDescription("按(ScannedTime,Id)稳定排序，包含锚点，最多11票。只返回真实时间与时序关联元数据，完整报文由包裹详情读取。")
            .Produces<ParcelTimingResponse>().ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        return routes;
    }
}
