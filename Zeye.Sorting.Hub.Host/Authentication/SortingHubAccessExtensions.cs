using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.Authentication;
/// <summary>注册可撤销的安全会话，并按部署配置保护业务接口。</summary>
public static class SortingHubAccessExtensions {
    /// <summary>注册真实账号认证；密钥保存在现有可写日志卷内，重启不会丢失会话密钥。</summary>
    public static IServiceCollection AddSortingHubAccess(this IServiceCollection services, string contentRoot) {
        services.AddScoped<AccessDirectoryService>();
        services.AddDataProtection().SetApplicationName("Zeye.Sorting.Hub").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(contentRoot, "logs", "data-protection")));
        services.AddAuthentication("SortingCookie").AddCookie("SortingCookie", options => {
            options.Cookie.Name = "Zeye.Sorting.Session"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async context => {
                // 探针公开且不使用账号权限；数据库故障时仍须返回真实的 503 健康报告。
                if (context.Request.Path.StartsWithSegments("/health")) return;
                var service = context.HttpContext.RequestServices.GetRequiredService<AccessDirectoryService>();
                var (directory, _) = await service.ReadAsync(context.HttpContext.RequestAborted);
                var user = directory.Users.SingleOrDefault(x => x.Id == context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier));
                var role = directory.Roles.SingleOrDefault(x => x.Id == user?.RoleId);
                if (user is null || !user.Enabled || role is null || user.SecurityStamp != context.Principal?.FindFirstValue("security-stamp")) { context.RejectPrincipal(); await context.HttpContext.SignOutAsync("SortingCookie"); return; }
                context.ReplacePrincipal(AccessDirectoryService.Principal(user, role));
            };
        });
        return services;
    }
    /// <summary>登录会话写请求校验来源标识；开启权限保护时，按真实角色权限保护 API。</summary>
    public static IApplicationBuilder UseSortingHubAccess(this IApplicationBuilder app) => app.Use(async (context, next) => {
        var path = (context.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) { await next(); return; }
        var service = context.RequestServices.GetRequiredService<AccessDirectoryService>();
        var write = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        if (context.User.Identity?.IsAuthenticated == true && write && context.Request.Headers["X-Zeye-Client"] != "web") {
            await Results.Problem(statusCode: 403, detail: "写入请求缺少来源校验标识。").ExecuteAsync(context); return;
        }
        // 手工新增仅用于管理员测试，即使关闭一般业务鉴权，也不能开放此入口。
        var normalizedPath = path.TrimEnd('/');
        var manualCreate = HttpMethods.IsPost(context.Request.Method) && (
            normalizedPath.Equals("/api/admin/parcels", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Equals("/api/admin/parcels/batch-buffer", StringComparison.OrdinalIgnoreCase));
        if (manualCreate) {
            if (context.User.Identity?.IsAuthenticated != true) { await Results.Problem(statusCode: 401, detail: "请先登录管理员账号。").ExecuteAsync(context); return; }
            if (!context.User.HasClaim("permission", "access.manage")) { await Results.Problem(statusCode: 403, detail: "手工创建包裹仅供管理员测试使用。").ExecuteAsync(context); return; }
            await next(); return;
        }
        if (!service.EnforceAuthorization || path is "/api/access/session" or "/api/access/login" or "/api/access/bootstrap") { await next(); return; }
        if (path == "/api/admin/parcels/processing-records" && service.MatchesSecret(context.Request.Headers["X-Sorting-Api-Key"], "Access:MachineApiKey")) { await next(); return; }
        if (context.User.Identity?.IsAuthenticated != true) { await Results.Problem(statusCode: 401, detail: "请先登录。").ExecuteAsync(context); return; }
        if (path is "/api/access/logout" or "/api/access/profile" or "/api/access/profile/avatar") { await next(); return; }
        var permission = path switch {
            _ when path.StartsWith("/api/admin/parcels/cleanup-history", StringComparison.Ordinal) => context.User.HasClaim("permission", "governance.manage") ? "governance.manage" : "audit.read",
            "/api/admin/parcels/cleanup-expired" => "governance.manage",
            _ when path.StartsWith("/api/access", StringComparison.Ordinal) => "access.manage",
            _ when path.StartsWith("/api/audit", StringComparison.Ordinal) => "audit.read",
            _ when path.StartsWith("/api/diagnostics", StringComparison.Ordinal) => "diagnostics.read",
            _ when path.StartsWith("/api/data-governance", StringComparison.Ordinal) => "governance.manage",
            _ when path.StartsWith("/api/operations/rules", StringComparison.Ordinal) => "rules.manage",
            _ when path.StartsWith("/api/operations/configuration", StringComparison.Ordinal) => write ? "access.manage" : "settings.read",
            _ when path.StartsWith("/api/operations", StringComparison.Ordinal) => "governance.manage",
            _ when path.StartsWith("/api/admin/parcels", StringComparison.Ordinal) => "parcels.write",
            _ when path.StartsWith("/api/parcels", StringComparison.Ordinal) => "parcels.read",
            _ => null
        };
        if (permission is null || !context.User.HasClaim("permission", permission)) { await Results.Problem(statusCode: 403, detail: "当前账号没有此操作权限。").ExecuteAsync(context); return; }
        await next();
    });
}
