using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NLog;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>处理事实写入与未关联DWS检索入口。</summary>
public static class ParcelProcessingApiRouteExtensions {
    /// <summary>入口异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>注册独立于Fusion通信方式的持久化接口。</summary>
    public static IEndpointRouteBuilder MapParcelProcessingApis(this IEndpointRouteBuilder routes) {
        routes.MapPost("/api/admin/parcels/processing-records", AppendAsync)
            .WithTags("Parcels-Admin").WithSummary("追加包裹处理事实并刷新快照")
            .WithDescription("以SourceInstanceId、SourceRunId、SourceParcelId识别一次过机，条码不参与身份。来源实例由部署配置；设备计数重置时更换SourceRunId，普通服务重启不更换。同一事实重试复用RecordId，不同执行尝试使用新RecordId；同一来源三元组的新检测记录返回409。身份字段拒绝首尾空白及控制字符。未知测量保留null，重量单位克、尺寸单位毫米、物理体积单位立方毫米。未关联DWS允许SourceParcelId为空。Stage范围0至10。时间仅允许本地时间。")
            .Produces<ParcelProcessingWriteResponse>(201).Produces<ParcelProcessingWriteResponse>(200).ProducesProblem(400).ProducesProblem(409).ProducesProblem(500);
        routes.MapGet("/api/parcels/processing-records/unbound", GetUnboundAsync)
            .WithTags("Parcels").WithSummary("查询最近未关联包裹的处理事实")
            .WithDescription("limit范围1至200，默认50；失败绑定与原始DWS信息保持可检索。")
            .Produces<IReadOnlyList<ParcelProcessingRecordResponse>>().ProducesProblem(400);
        return routes;
    }

    /// <summary>返回首次写入、幂等重复、身份冲突或验证错误的明确结果。</summary>
    private static async Task<IResult> AppendAsync([FromBody] ParcelProcessingRecordRequest request, ParcelProcessingApplicationService service, CancellationToken cancellationToken) {
        try {
            var result = await service.AppendAsync(request, cancellationToken);
            if (!result.IsSuccess) return Results.Problem(result.ErrorMessage, statusCode: result.ErrorCode is "ParcelProcessingConflict" or "ParcelSourceConflict" ? 409 : 500);
            return result.Value!.IsDuplicate ? Results.Ok(result.Value) : Results.Json(result.Value, statusCode: 201);
        }
        catch (Exception ex) when (ex is ArgumentException or ValidationException) { Logger.Warn(ex, "处理记录输入无效"); return Results.Problem(ex.Message, statusCode: 400); }
    }

    /// <summary>读取受数量限制的未关联记录。</summary>
    private static async Task<IResult> GetUnboundAsync(ParcelProcessingApplicationService service, CancellationToken cancellationToken, int limit = 50) {
        try { return Results.Ok(await service.GetUnboundAsync(limit, cancellationToken)); }
        catch (ArgumentException ex) { Logger.Warn(ex, "未关联处理事实查询参数无效"); return Results.Problem(ex.Message, statusCode: 400); }
    }
}
