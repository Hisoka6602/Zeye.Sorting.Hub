using System.Text.Json;
using System.Text.Json.Nodes;
using LiteDB;
using Microsoft.Data.Sqlite;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>超级管理员维护 LiteDB 当前配置和关系库中的原值变更历史。</summary>
public static class RuntimeConfigurationApi {
    /// <summary>版本冲突、输入错误和存储异常诊断，不输出请求正文。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>注册受超级管理员权限保护的配置和历史接口。</summary>
    public static void MapRuntimeConfigurationApis(this WebApplication app) {
        var group = app.MapGroup("/api/operations/configuration").WithTags("Configuration");
        group.AddEndpointFilter(async (invocation, next) => {
            var context = invocation.HttpContext;
            context.Response.Headers.CacheControl = "private, no-store";
            if (context.User.Identity?.IsAuthenticated != true) return Results.Problem(statusCode: 401, detail: "请先登录超级管理员账号。");
            if (!AccessDirectoryService.IsSuperAdministrator(context.User)) return Results.Problem(statusCode: 403, detail: "需要超级管理员权限。");
            try { return await next(invocation); }
            catch (ConfigurationConflictException exception) { Logger.Debug(exception, "配置版本冲突，TraceId={TraceId}", context.TraceIdentifier); return Results.Problem(statusCode: 409, detail: "配置已更新，请刷新后重试。"); }
            catch (ArgumentException exception) { Logger.Debug(exception, "配置输入校验失败，TraceId={TraceId}", context.TraceIdentifier); return Results.Problem(statusCode: 400, detail: exception.Message); }
            catch (JsonException exception) { Logger.Warn(exception, "配置 JSON 解析失败，TraceId={TraceId}", context.TraceIdentifier); return Results.Problem(statusCode: 400, detail: "配置 JSON 格式无效。"); }
            catch (Exception exception) when (exception is IOException or LiteException or SqliteException) {
                Logger.Error(exception, "配置存储访问失败，TraceId={TraceId}", context.TraceIdentifier);
                return Results.Problem(statusCode: 503, detail: "配置存储暂不可用，请检查目录权限和磁盘空间。");
            }
        });
        group.MapGet("/runtime", (RuntimeConfigurationProvider source) => {
            source.TryReload();
            return Results.Ok(source.Capture());
        }).WithSummary("读取运行配置及生效状态")
            .WithDescription("仅超级管理员可读取配置原值、版本、环境覆盖项和待重启参数；Fusion 来源目录由接入配置接口维护。");
        group.MapPut("/runtime", (RuntimeConfigurationUpdate request, RuntimeConfigurationProvider source) => {
            if (string.IsNullOrWhiteSpace(request.Revision) || request.Changes is null) return Results.Problem(statusCode: 400, detail: "缺少配置版本或修改内容。");
            var saved = source.Save(request.Revision, request.Changes);
            return Results.Ok(new { result = saved, snapshot = source.Capture() });
        }).WithSummary("按版本保存运行配置")
            .WithDescription("整体替换数组，先校验并耐久保存，再通知热更新；数据库连接、监听地址等启动参数下次启动生效。");
        group.MapGet("/history", (int? limit, ConfigurationHistoryStore history) => Results.Ok(history.Read(limit ?? 100)))
            .WithSummary("读取配置变更历史")
            .WithDescription("仅超级管理员可读取修改前后原值和提交状态，最多 500 条；旧版已脱敏历史保留原记录。");
    }
}
