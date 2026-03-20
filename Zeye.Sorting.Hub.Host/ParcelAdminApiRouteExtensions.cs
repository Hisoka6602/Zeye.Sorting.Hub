using Microsoft.AspNetCore.Mvc;
using NLog;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// Parcel 管理端写接口路由扩展。
/// 说明：
///   - 普通写接口（POST/PUT/DELETE）走 Application 层应用服务，不直接操作仓储实体。
///   - 危险治理接口（cleanup-expired）必须明确声明为治理端点，由仓储内置隔离器决策。
/// 鉴权预留：当引入鉴权体系（如 JWT/API-Key/RBAC）时，在 MapGroup 上追加
///   .RequireAuthorization("AdminPolicy") 即可统一覆盖本组所有端点。
/// </summary>
public static class ParcelAdminApiRouteExtensions {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册 Parcel 管理端 API（普通写接口 + 危险治理接口）。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapParcelAdminApis(this IEndpointRouteBuilder routeBuilder) {
        // 管理端路由分组，统一前缀 /api/admin/parcels。
        // 预留鉴权接入点：此处可添加 .RequireAuthorization("AdminPolicy") 以启用授权保护。
        var group = routeBuilder.MapGroup("/api/admin/parcels").WithTags("Parcels-Admin");

        // ── 普通写接口 ──────────────────────────────────────────────────────────────
        group.MapPost(string.Empty, CreateParcelAsync)
            .WithName("AdminCreateParcel")
            .WithSummary("管理端新增 Parcel")
            .Produces<ParcelDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:long}", UpdateParcelStatusAsync)
            .WithName("AdminUpdateParcelStatus")
            .WithSummary("管理端更新 Parcel 状态（支持标记完结/分拣异常/更新接口状态）")
            .Produces<ParcelDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:long}", DeleteParcelAsync)
            .WithName("AdminDeleteParcel")
            .WithSummary("管理端删除单个 Parcel")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ── 危险治理接口（单独分组，明确区分于普通业务端点）─────────────────────────
        group.MapPost("/cleanup-expired", CleanupExpiredParcelsAsync)
            .WithName("AdminCleanupExpiredParcels")
            .WithSummary("[治理接口] 触发过期包裹清理（由仓储隔离器决策 blocked/dry-run/execute，不可绕过）")
            .Produces<ParcelCleanupExpiredResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return routeBuilder;
    }

    /// <summary>
    /// 处理新增包裹请求。
    /// </summary>
    /// <param name="request">新增包裹请求合同（JSON body）。</param>
    /// <param name="commandService">新增包裹应用服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>201 Created + 新增后的详情，或 400 Bad Request。</returns>
    private static async Task<IResult> CreateParcelAsync(
        [Microsoft.AspNetCore.Mvc.FromBody] ParcelCreateRequest request,
        CreateParcelCommandService commandService,
        CancellationToken cancellationToken) {
        if (request is null) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "请求体不能为空。");
        }

        // 步骤 1：在 Host 层解析时间字符串并强制拒绝 UTC/offset 表达（字符串方式可彻底拒绝 +offset 表达）。
        if (!LocalDateTimeParsing.TryParseLocalDateTime(request.ScannedTime, out var scannedTime)) {
            return LocalDateTimeParsing.CreateBadRequestProblem(
                "请求参数无效",
                "scannedTime 必须是本地时间格式（如 yyyy-MM-dd HH:mm:ss），不允许 UTC 或时区 offset。");
        }

        if (!LocalDateTimeParsing.TryParseLocalDateTime(request.DischargeTime, out var dischargeTime)) {
            return LocalDateTimeParsing.CreateBadRequestProblem(
                "请求参数无效",
                "dischargeTime 必须是本地时间格式（如 yyyy-MM-dd HH:mm:ss），不允许 UTC 或时区 offset。");
        }

        try {
            // 步骤 2：调用 Application 服务，传入已解析的本地时间（服务无需感知 HTTP 时间格式）。
            var response = await commandService.ExecuteAsync(request, scannedTime, dischargeTime, cancellationToken);
            return Results.Created($"/api/admin/parcels/{response.Id}", response);
        }
        catch (ArgumentException ex) {
            Logger.Warn(ex, "新增 Parcel 参数校验失败。");
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", ex.Message);
        }
        catch (InvalidOperationException ex) {
            Logger.Error(ex, "新增 Parcel 业务逻辑异常。");
            return Results.Problem(
                title: "新增失败",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 处理更新包裹状态请求。
    /// </summary>
    /// <param name="id">目标包裹 Id（路由参数）。</param>
    /// <param name="request">更新状态请求合同（JSON body）。</param>
    /// <param name="commandService">更新包裹状态应用服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 OK + 更新后的详情，或 404 Not Found，或 400 Bad Request。</returns>
    private static async Task<IResult> UpdateParcelStatusAsync(
        long id,
        [Microsoft.AspNetCore.Mvc.FromBody] ParcelUpdateRequest request,
        UpdateParcelStatusCommandService commandService,
        CancellationToken cancellationToken) {
        if (request is null) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "请求体不能为空。");
        }

        // 步骤 1：若提供了 CompletedTime 字符串，在 Host 层解析并强制拒绝 UTC/offset 表达。
        //         不解析则传 null，由 Application 服务在 MarkCompleted 操作时报错。
        DateTime? completedTime = null;
        if (request.CompletedTime is not null) {
            if (!LocalDateTimeParsing.TryParseLocalDateTime(request.CompletedTime, out var parsedCompletedTime)) {
                return LocalDateTimeParsing.CreateBadRequestProblem(
                    "请求参数无效",
                    "completedTime 必须是本地时间格式（如 yyyy-MM-dd HH:mm:ss），不允许 UTC 或时区 offset。");
            }

            completedTime = parsedCompletedTime;
        }

        try {
            // 步骤 2：调用 Application 服务，传入已解析的本地完结时间（服务无需感知 HTTP 时间格式）。
            var response = await commandService.ExecuteAsync(id, request, completedTime, cancellationToken);
            return response is null
                ? Results.Problem(
                    title: "资源不存在",
                    detail: $"未找到 Id 为 {id} 的包裹。",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(response);
        }
        catch (ArgumentException ex) {
            Logger.Warn(ex, "更新 Parcel 状态参数校验失败，Id={ParcelId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", ex.Message);
        }
        catch (InvalidOperationException ex) {
            Logger.Error(ex, "更新 Parcel 状态业务逻辑异常，Id={ParcelId}", id);
            return Results.Problem(
                title: "更新失败",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 处理删除包裹请求。
    /// </summary>
    /// <param name="id">目标包裹 Id（路由参数）。</param>
    /// <param name="commandService">删除包裹应用服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>204 No Content，或 404 Not Found，或 400 Bad Request。</returns>
    private static async Task<IResult> DeleteParcelAsync(
        long id,
        DeleteParcelCommandService commandService,
        CancellationToken cancellationToken) {
        try {
            var deleted = await commandService.ExecuteAsync(id, cancellationToken);
            return deleted
                ? Results.NoContent()
                : Results.Problem(
                    title: "资源不存在",
                    detail: $"未找到 Id 为 {id} 的包裹。",
                    statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex) {
            Logger.Warn(ex, "删除 Parcel 参数校验失败，Id={ParcelId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", ex.Message);
        }
        catch (InvalidOperationException ex) {
            Logger.Error(ex, "删除 Parcel 业务逻辑异常，Id={ParcelId}", id);
            return Results.Problem(
                title: "删除失败",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// 处理过期包裹清理请求（治理型端点，不得绕过仓储隔离器）。
    /// 鉴权预留：此端点应在生产环境配置更严格的鉴权（如管理员角色或 API-Key），
    ///   建议在 MapGroup 或此端点上追加 .RequireAuthorization("DangerousActionPolicy")。
    /// </summary>
    /// <param name="request">清理请求合同（JSON body，含 createdBefore 本地时间字符串）。</param>
    /// <param name="commandService">过期清理应用服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>200 OK + 清理治理响应（含决策/计划量/执行量/补偿边界），或 400 Bad Request。</returns>
    private static async Task<IResult> CleanupExpiredParcelsAsync(
        [Microsoft.AspNetCore.Mvc.FromBody] ParcelCleanupExpiredRequest request,
        CleanupExpiredParcelsCommandService commandService,
        CancellationToken cancellationToken) {
        if (request is null) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "请求体不能为空。");
        }

        // 步骤 1：解析 createdBefore 为本地时间，拒绝 UTC/offset 表达。
        if (!LocalDateTimeParsing.TryParseLocalDateTime(request.CreatedBefore, out var createdBefore)) {
            return LocalDateTimeParsing.CreateBadRequestProblem(
                "请求参数无效",
                "createdBefore 必须是本地时间格式（如 yyyy-MM-dd HH:mm:ss），且不允许包含 UTC 或时区偏移。");
        }

        try {
            // 步骤 2：调用应用服务（内部不绕过仓储隔离器）。
            var response = await commandService.ExecuteAsync(createdBefore, cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex) {
            Logger.Error(ex, "[治理] 过期包裹清理应用服务异常，CreatedBefore={CreatedBefore}", request.CreatedBefore);
            return Results.Problem(
                title: "清理执行失败",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
