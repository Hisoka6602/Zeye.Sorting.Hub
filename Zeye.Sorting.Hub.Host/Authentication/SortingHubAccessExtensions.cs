using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.Authentication;
/// <summary>注册可撤销的安全会话，并按部署配置保护业务接口。</summary>
public static class SortingHubAccessExtensions {
    /// <summary>注册真实账号认证；密钥与配置文件同卷保存，兼容旧日志目录的会话密钥。</summary>
    public static IServiceCollection AddSortingHubAccess(this IServiceCollection services, string contentRoot, string? configurationDatabasePath = null) {
        services.AddScoped<AccessDirectoryService>();
        services.AddDataProtection().SetApplicationName("Zeye.Sorting.Hub").PersistKeysToFileSystem(DataProtectionKeyStorage.Prepare(contentRoot, configurationDatabasePath));
        services.AddAuthentication("SortingCookie").AddCookie("SortingCookie", options => {
            options.Cookie.Name = "Zeye.Sorting.Session"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async context => {
                // 存活与就绪探针保持公开且不依赖账号数据库；深度诊断重新验证当前身份。
                if (context.Request.Path.Equals(new PathString("/health/live")) || context.Request.Path.Equals(new PathString("/health/ready"))) return;
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
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) && path != "/health/deep") { await next(); return; }
        var service = context.RequestServices.GetRequiredService<AccessDirectoryService>();
        var write = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        if (context.User.Identity?.IsAuthenticated == true && write && context.Request.Headers["X-Zeye-Client"] != "web") {
            await Results.Problem(statusCode: 403, detail: "写入请求缺少来源校验标识。").ExecuteAsync(context); return;
        }
        // 自动上报继续使用专用机器密钥；该凭据不能读取或操作其他敏感版块。
        if (path == "/api/admin/parcels/processing-records" && HttpMethods.IsPost(context.Request.Method)
            && service.MatchesSecret(context.Request.Headers["X-Sorting-Api-Key"], "Access:MachineApiKey")) { await next(); return; }
        // 测试、治理与可观测性在通用鉴权关闭时也不得对普通账号或匿名请求开放。
        if (RequiresSuperAdministrator(path, context.Request.Method)) {
            if (context.User.Identity?.IsAuthenticated != true) { await Results.Problem(statusCode: 401, detail: "请先登录超级管理员账号。").ExecuteAsync(context); return; }
            if (!AccessDirectoryService.IsSuperAdministrator(context.User)) { await Results.Problem(statusCode: 403, detail: "当前版块仅限超级管理员或内置超级用户访问。").ExecuteAsync(context); return; }
            if (path == "/health/deep") { await next(); return; }
        }
        if (!service.EnforceAuthorization || path is "/api/access/session" or "/api/access/login" or "/api/access/bootstrap") { await next(); return; }
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
    /// <summary>按正式入口识别敏感版块，覆盖读取、写入、历史明细和深度诊断。</summary>
    private static bool RequiresSuperAdministrator(string path, string method) => path == "/health/deep"
        || path == "/api/admin/parcels/cleanup-expired" || path.StartsWith("/api/admin/parcels/cleanup-history", StringComparison.Ordinal)
        || path == "/api/audit" || path.StartsWith("/api/audit/", StringComparison.Ordinal)
        || path == "/api/diagnostics" || path.StartsWith("/api/diagnostics/", StringComparison.Ordinal)
        || path == "/api/data-governance" || path.StartsWith("/api/data-governance/", StringComparison.Ordinal)
        || path == "/api/operations/partitions" || path.StartsWith("/api/operations/partitions/", StringComparison.Ordinal)
        || HttpMethods.IsPost(method) && path is "/api/admin/parcels" or "/api/admin/parcels/batch-buffer" or "/api/admin/parcels/processing-records";
}
