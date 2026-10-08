using Microsoft.AspNetCore.Mvc;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>登记DWS重复测量分析，继承包裹读取权限。</summary>
public static class ParcelDwsConsistencyApiRouteExtensions {
    /// <summary>按精确条码比较重复重量、物理体积和扫码耗时，提供排行、来源对比和量测下钻。</summary>
    public static IEndpointRouteBuilder MapParcelDwsConsistencyApis(this IEndpointRouteBuilder routes) {
        routes.MapGet("/api/parcels/dws-consistency", ReadAsync).WithTags("Parcels").WithName("GetParcelDwsConsistency")
            .WithSummary("分析同条码的DWS重复测量一致性")
            .WithDescription("按记录首次入库本地日期读取DWS窄列事实，最长31天。同来源实例、运行会话和消息身份的接收与绑定只算一次量测，缺少身份或同身份数值冲突的记录明确排除。分别返回重量（克）、物理体积（立方厘米）及扫码耗时（毫秒）的最小值、最大值、平均值、中位数、P95、极差和偏差排行；绝对差与相对差均超过阈值才标记超差。扫码耗时使用同一明确来源包裹的检测时间至来源程序接收到有效条码的时间，不混用设备量测、绑定、扫描上传接口或Hub入库时间，缺失或倒序时间保持未知。按来源工作台对比、分页量测明细、每种趋势最多200个真实时间点；可对指定条码输入标准重量或体积参考值，仅用于本次查询。复用一分钟只读快照，显式刷新重新读取。")
            .Produces<ParcelDwsConsistencyResponse>().ProducesProblem(400);
        return routes;
    }
    /// <summary>严格解析本地日期与阈值，不接受UTC或带时间的日期。</summary>
    private static async Task<IResult> ReadAsync([AsParameters] ParcelDwsConsistencyQueryParameters query,
        [FromServices] IParcelDwsConsistencyReadService service, CancellationToken token) {
        if (query.FromDate?.Length != 10 || query.ToDate?.Length != 10
            || !LocalDateTimeParsing.TryParseLocalDateTime(query.FromDate, out var from)
            || !LocalDateTimeParsing.TryParseLocalDateTime(query.ToDate, out var to))
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "fromDate/toDate 必须是yyyy-MM-dd本地日期。");
        try {
            return Results.Ok(await service.ReadAsync(new() {
                FromDate = from, ToDate = to, Barcode = query.Barcode, SourceInstanceId = query.SourceInstanceId, WorkstationName = query.WorkstationName,
                WeightToleranceGrams = query.WeightToleranceGrams ?? 20m, WeightTolerancePercent = query.WeightTolerancePercent ?? 2m,
                VolumeToleranceCm3 = query.VolumeToleranceCm3 ?? 100m, VolumeTolerancePercent = query.VolumeTolerancePercent ?? 3m,
                ScanDurationToleranceMilliseconds = query.ScanDurationToleranceMilliseconds ?? 50m, ScanDurationTolerancePercent = query.ScanDurationTolerancePercent ?? 20m,
                ReferenceWeightGrams = query.ReferenceWeightGrams, ReferenceVolumeCm3 = query.ReferenceVolumeCm3,
                OnlyDeviations = query.OnlyDeviations ?? false, SortBy = query.SortBy ?? "weight", PageNumber = query.PageNumber ?? 1,
                DetailBarcode = query.DetailBarcode, MeasurementPageNumber = query.MeasurementPageNumber ?? 1, Refresh = query.Refresh ?? false
            }, token));
        }
        catch (ArgumentException exception) { return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
    }
}
