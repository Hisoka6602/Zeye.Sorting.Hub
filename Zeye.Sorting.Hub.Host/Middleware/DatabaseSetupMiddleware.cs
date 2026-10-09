using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.Hosting;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>在认证和数据库审计之前提供受访问码保护的本机数据库配置，并隔离未就绪业务。</summary>
public sealed class DatabaseSetupMiddleware(RequestDelegate next, DatabaseStartupState state) {
    /// <summary>允许本机引导修改的唯一字段白名单。</summary>
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase) {
        "Persistence:Provider", "Persistence:MigrationGovernance:DryRun", "ConnectionStrings:MySql", "ConnectionStrings:MySqlReadOnly",
        "ConnectionStrings:SqlServer", "ConnectionStrings:SqlServerReadOnly", "ConnectionStrings:Oracle", "ConnectionStrings:OracleAdministration",
        "ConnectionStrings:OracleReadOnly", "ConnectionStrings:SQLite", "ConnectionStrings:SQLiteReadOnly"
    };
    /// <summary>配置错误日志，不输出请求正文或访问码。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>处理启动状态和受限配置，其他未就绪 API 明确返回 503。</summary>
    public async Task InvokeAsync(HttpContext context, RuntimeConfigurationProvider source) {
        var path = (context.Request.Path.Value ?? "").TrimEnd('/');
        if (path.Equals("/api/setup/status", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsGet(context.Request.Method)) {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Json(new { instanceId = state.InstanceId, ready = state.Ready, requiresConfiguration = state.RequiresConfiguration,
                localSetupAllowed = DatabaseStartupState.IsLocal(context),
                failureSummary = DatabaseStartupState.IsLocal(context) ? state.FailureSummary : null,
                setupKeyPath = DatabaseStartupState.IsLocal(context) && state.RequiresConfiguration ? state.SetupKeyPath : null }).ExecuteAsync(context);
            return;
        }
        if (path.StartsWith("/api/setup/", StringComparison.OrdinalIgnoreCase)) {
            await ConfigureDatabaseAsync(context, source, path);
            return;
        }
        if (!state.Ready && (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/fusion/", StringComparison.OrdinalIgnoreCase))) {
            await Results.Problem(statusCode: 503, title: "数据库尚未就绪", detail: "请在服务器本机完成数据库配置并重启服务。").ExecuteAsync(context);
            return;
        }
        if (!state.Ready && path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase)) {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            var report = new HealthReport(new Dictionary<string, HealthReportEntry> {
                ["database-startup"] = new(HealthStatus.Unhealthy, "数据库配置或初始化尚未完成。", TimeSpan.Zero, null, null)
            }, TimeSpan.Zero);
            await HealthCheckResponseWriter.WriteJsonResponseAsync(context, report);
            return;
        }
        await next(context);
    }

    /// <summary>验证本机、来源和访问码；版本化保存只包含数据库字段的修改。</summary>
    private async Task ConfigureDatabaseAsync(HttpContext context, RuntimeConfigurationProvider source, string path) {
        context.Response.Headers.CacheControl = "private, no-store";
        var restarting = path.Equals("/api/setup/host/restart", StringComparison.OrdinalIgnoreCase);
        if (!state.RequiresConfiguration || !restarting && !path.Equals("/api/setup/database/runtime", StringComparison.OrdinalIgnoreCase)) {
            context.Response.StatusCode = 404; return;
        }
        if (!DatabaseStartupState.IsLocalWrite(context)) {
            await Results.Problem(statusCode: 403, detail: "数据库配置入口只允许服务器本机直接访问。").ExecuteAsync(context); return;
        }
        if (!state.Authorize(context.Request.Headers["X-Zeye-Setup-Key"].ToString())) {
            await Results.Problem(statusCode: 401, detail: "请使用本次启动生成的本机配置访问码。").ExecuteAsync(context); return;
        }
        try {
            if (restarting) {
                if (!HttpMethods.IsPost(context.Request.Method)) { context.Response.StatusCode = 405; return; }
                var restartRequest = await context.Request.ReadFromJsonAsync<HostRestartRequest>(context.RequestAborted);
                await context.RequestServices.GetRequiredService<HostRestartCoordinator>()
                    .RequestRestart(context, restartRequest?.Revision, source, state).ExecuteAsync(context);
                return;
            }
            source.TryReload();
            if (HttpMethods.IsGet(context.Request.Method)) { await Results.Json(SelectDatabase(source.Capture())).ExecuteAsync(context); return; }
            if (!HttpMethods.IsPut(context.Request.Method)) { context.Response.StatusCode = 405; return; }
            var request = await context.Request.ReadFromJsonAsync<RuntimeConfigurationUpdate>(context.RequestAborted);
            if (request?.Changes is null || string.IsNullOrWhiteSpace(request.Revision)
                || request.Changes.Any(item => !item.Key.Equals("Persistence", StringComparison.OrdinalIgnoreCase)
                    && !item.Key.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
                || request.Changes.Any(item => item.Value is not JsonObject)
                || ConfigurationDocument.Flatten(request.Changes).Keys.Any(key => !AllowedKeys.Contains(key))) {
                await Results.Problem(statusCode: 400, detail: "仅允许修改数据库类型、连接字符串和初始化预演开关，且必须提供当前配置版本。").ExecuteAsync(context); return;
            }
            var result = source.Save(request.Revision, request.Changes);
            await Results.Json(new { result, snapshot = SelectDatabase(source.Capture()) }).ExecuteAsync(context);
        }
        catch (ConfigurationConflictException exception) {
            Logger.Debug(exception, "本机数据库配置版本冲突。");
            await Results.Problem(statusCode: 409, detail: "配置已更新，请刷新后重试。").ExecuteAsync(context);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException) {
            Logger.Warn(exception, "本机数据库配置输入无效。");
            await Results.Problem(statusCode: 400, detail: "数据库配置格式或参数无效，请检查后重试。").ExecuteAsync(context);
        }
    }

    /// <summary>受限入口只返回数据库配置，不泄露账号、设备密钥和原值历史。</summary>
    private static RuntimeConfigurationState SelectDatabase(RuntimeConfigurationState snapshot) {
        var connections = new JsonObject();
        if (snapshot.Configuration["ConnectionStrings"] is JsonObject values)
            foreach (var item in values.Where(item => AllowedKeys.Contains("ConnectionStrings:" + item.Key))) connections[item.Key] = item.Value?.DeepClone();
        var selected = new JsonObject { ["Persistence"] = new JsonObject {
                ["Provider"] = snapshot.Configuration["Persistence"]?["Provider"]?.DeepClone(),
                ["MigrationGovernance"] = new JsonObject { ["DryRun"] = snapshot.Configuration["Persistence"]?["MigrationGovernance"]?["DryRun"]?.DeepClone() }
            },
            ["ConnectionStrings"] = connections };
        var effective = new JsonObject();
        foreach (var item in snapshot.EffectiveConfiguration.Where(item => AllowedKeys.Contains(item.Key))) effective[item.Key] = item.Value?.DeepClone();
        return snapshot with { Configuration = selected, EffectiveConfiguration = effective,
            OverriddenKeys = snapshot.OverriddenKeys.Where(AllowedKeys.Contains).ToArray(),
            RestartRequiredKeys = snapshot.RestartRequiredKeys.Where(AllowedKeys.Contains).ToArray(), HotReloadKeys = [], LastReloadError = null };
    }
}
