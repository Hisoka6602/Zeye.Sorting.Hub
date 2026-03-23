using Microsoft.AspNetCore.Mvc;
using NLog;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;

namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// Parcel 只读 API 路由扩展。
/// </summary>
public static class ParcelReadOnlyApiRouteExtensions {

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册 Parcel 只读 API。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapParcelReadOnlyApis(this IEndpointRouteBuilder routeBuilder) {
        var group = routeBuilder.MapGroup("/api/parcels").WithTags("Parcels");

        group.MapGet(string.Empty, GetParcelListAsync)
            .WithName("GetParcelList")
            .WithSummary("分页查询 Parcel 列表")
            .WithDescription("按页码、分页大小与可选过滤条件（条码检索词、集包号、状态、异常类型、格口、扫码时间范围）查询包裹摘要列表。时间参数必须为本地时间字符串，不允许 UTC 或时区偏移。\n\nBarCodeKeyword Provider 语义：MySQL 使用 FULLTEXT Boolean 模式，其他 Provider 使用 Contains 子串匹配。")
            .Produces<ParcelListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:long}", GetParcelByIdAsync)
            .WithName("GetParcelById")
            .WithSummary("按 Id 查询 Parcel 详情")
            .WithDescription("按包裹主键查询完整详情；当包裹不存在时返回 404。")
            .Produces<ParcelDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/adjacent", GetAdjacentParcelsAsync)
            .WithName("GetAdjacentParcels")
            .WithSummary("按包裹 Id 查询 Parcel 邻近记录")
            .WithDescription("以指定包裹 Id 为锚点，基于稳定排序键 (ScannedTime, Id) 查询前后邻近记录数量。锚点不存在返回 404。")
            .Produces<ParcelAdjacentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routeBuilder;
    }

    /// <summary>
    /// 处理列表查询请求。
    /// </summary>
    /// <param name="query">列表查询参数。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetParcelListAsync(
        [AsParameters] ParcelListQueryParameters query,
        GetParcelPagedQueryService queryService,
        CancellationToken cancellationToken) {
        if (!LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.ScannedTimeStart, out var scannedTimeStart)
            || !LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.ScannedTimeEnd, out var scannedTimeEnd)) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "scannedTimeStart/scannedTimeEnd 必须是本地时间格式，且不允许包含 UTC 或时区偏移。");
        }

        try {
            var request = new ParcelListRequest {
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                BarCodeKeyword = query.BarCodeKeyword,
                BagCode = query.BagCode,
                WorkstationName = query.WorkstationName,
                Status = query.Status,
                ExceptionType = query.ExceptionType,
                ActualChuteId = query.ActualChuteId,
                TargetChuteId = query.TargetChuteId,
                ScannedTimeStart = scannedTimeStart,
                ScannedTimeEnd = scannedTimeEnd
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 列表查询参数校验失败。");
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理详情查询请求。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetParcelByIdAsync(
        long id,
        GetParcelByIdQueryService queryService,
        CancellationToken cancellationToken) {
        try {
            var response = await queryService.ExecuteAsync(id, cancellationToken);
            return response is null
                ? LocalDateTimeParsing.CreateParcelMissingProblem(id)
                : Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 详情查询参数校验失败，Id={ParcelId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理邻近查询请求。
    /// </summary>
    /// <param name="query">邻近查询参数。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetAdjacentParcelsAsync(
        [AsParameters] ParcelAdjacentQueryParameters query,
        GetAdjacentParcelsQueryService queryService,
        CancellationToken cancellationToken) {
        if (!query.Id.HasValue) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "id 为必填参数。");
        }

        try {
            var request = new ParcelAdjacentRequest {
                Id = query.Id.Value,
                BeforeCount = query.BeforeCount,
                AfterCount = query.AfterCount
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException exception) {
            Logger.Warn(exception, "Parcel 邻近查询锚点不存在，Id={ParcelId}", query.Id);
            return LocalDateTimeParsing.CreateParcelMissingProblem(query.Id.Value);
        }
        catch (ArgumentException exception) {
            Logger.Warn(
                exception,
                "Parcel 邻近查询参数校验失败，Id={ParcelId}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                query.Id,
                query.BeforeCount,
                query.AfterCount);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

}
