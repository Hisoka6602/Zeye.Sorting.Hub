using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Hubs;
namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>管理员专用的低频配置写入，读取响应和实时快照始终不包含密钥。</summary>
public static class FusionConfigurationApi {
    /// <summary>记录配置请求异常，保留请求路径和追踪编号。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>注册管理员专用的接入目录管理接口。</summary>
    public static void MapFusionConfiguration(this WebApplication app) {
        var group = app.MapGroup("/api/operations/configuration/fusion");
        group.AddEndpointFilter(async (invocation, next) => {
            var context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "private, no-store";
            if (context.User.Identity?.IsAuthenticated != true) return Results.Problem(statusCode: 401, detail: "请先登录。");
            if (!context.User.HasClaim("permission", "access.manage")) return Results.Problem(statusCode: 403, detail: "需要接入配置管理权限。");
            try { return await next(invocation); }
            catch (ArgumentException exception) {
                Logger.Debug(exception, "Fusion 配置请求无效，Path={Path}, TraceId={TraceId}", context.Request.Path, context.TraceIdentifier);
                return Results.Problem(statusCode: 400, detail: exception.Message);
            }
        });
        /// <summary>返回目录版本冲突，不覆盖他人的修改。</summary>
        static IResult Conflict() => Results.Problem(statusCode: 409, detail: "配置已变更，请刷新后重试，未覆盖其他修改。");
        group.MapGet("", async (FusionConfigurationService service, CancellationToken ct) => Results.Ok(await service.ReadAsync(ct)))
            .WithSummary("读取 Fusion 接入配置与来源目录").WithDescription("返回 Hub 身份、工作台发现设置、来源目录及版本，供具有接入配置管理权限的用户查看和编辑；读取响应不包含机器认证密钥。");
        group.MapPut("", async (FusionSettingsWrite request, FusionConfigurationService service, RealtimeResourceSignal changes, CancellationToken ct) => {
            if (request.Settings is null) return Results.Problem(statusCode: 400, detail: "缺少接入配置。");
            if (await service.WriteSettingsAsync(request.Revision, request.Settings, ct) is null) return Conflict();
            changes.Notify(); return Results.Ok(await service.ReadAsync(ct));
        }).WithSummary("保存 Fusion 接入配置")
            .WithDescription("按目录版本保存 Hub 身份与工作台发现设置，持久保存并通知订阅更新；输入无效或配置存在并发修改时拒绝覆盖。");
        group.MapPost("/sources", async (FusionSourceChange request, FusionConfigurationService service, RealtimeResourceSignal changes, CancellationToken ct) => {
            if (request.Source is null) return Results.Problem(statusCode: 400, detail: "缺少工作台配置。");
            var result = await service.CreateSourceAsync(request.Revision, request.Source, ct);
            if (result is null) return Conflict();
            changes.Notify(); return Results.Ok(result);
        }).WithSummary("登记新的 Fusion 来源工作台")
            .WithDescription("按目录版本登记来源实例、名称及产线设备身份，生成独立机器认证密钥并返回配对信息；密钥仅在创建响应中展示，版本冲突时不创建来源。");
        group.MapPut("/sources/{id}", async (string id, FusionSourceChange request, FusionConfigurationService service, RealtimeResourceSignal changes, CancellationToken ct) => {
            if (request.Source is null) return Results.Problem(statusCode: 400, detail: "缺少工作台配置。");
            if (await service.UpdateSourceAsync(request.Revision, id, request.Source, ct) is null) return Conflict();
            changes.Notify(); return Results.Ok(await service.ReadAsync(ct));
        }).WithSummary("更新 Fusion 来源工作台配置")
            .WithDescription("按目录版本维护指定来源工作台的名称、身份及启用状态，并通知实时订阅；已经登记的来源身份受锁定约束，不返回机器认证密钥。");
        group.MapPost("/sources/{id}/rotate-key", async (string id, FusionKeyRotation request, FusionConfigurationService service, RealtimeResourceSignal changes, CancellationToken ct) => {
            var result = await service.RotateKeyAsync(request.Revision, id, ct);
            if (result is null) return Conflict();
            changes.Notify(); return Results.Ok(result);
        }).WithSummary("轮换 Fusion 来源机器认证密钥")
            .WithDescription("为指定来源生成新的机器认证密钥和配对信息，使旧凭据失效并通知连接更新；新密钥仅在本次响应中展示，目录版本冲突时不轮换。");
    }
}
