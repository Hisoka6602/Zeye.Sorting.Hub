using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>工作台窗口汇总读取入口，沿用parcels.read权限及实时只读通道。</summary>
public static class ParcelWorkbenchApiRouteExtensions {
    /// <summary>窗口固定为服务器本地滚动24小时，不受最新明细200票限制。</summary>
    public static IEndpointRouteBuilder MapParcelWorkbenchApis(this IEndpointRouteBuilder routes) {
        routes.MapGet("/api/parcels/workbench", async (IParcelWorkbenchReadService service, CancellationToken token) =>
            Results.Ok(await service.GetAsync(DateTime.Now, token)))
            .WithTags("Parcels").WithName("GetParcelWorkbench")
            .WithSummary("按来源汇总完整滚动24小时包裹")
            .WithDescription("按ScannedTime包含两端筛选，完整数据库聚合；来源实例优先于工作台名称，编号会话不拆组，重复条码照常计数。")
            .Produces<ParcelWorkbenchResponse>();
        return routes;
    }
}
