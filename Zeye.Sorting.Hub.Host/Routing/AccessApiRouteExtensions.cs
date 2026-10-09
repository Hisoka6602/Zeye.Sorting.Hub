using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Security;
namespace Zeye.Sorting.Hub.Host.Routing;
/// <summary>真实账号初始化、登录、退出及角色和用户维护接口。</summary>
public static class AccessApiRouteExtensions {
    /// <summary>按已认证管理员权限判断账号维护入口。</summary>
    private static bool IsAdmin(HttpContext context) => context.User.HasClaim("permission", "access.manage");
    /// <summary>读取请求字符串；空值统一为无输入。</summary>
    private static string Text(JsonElement json, string key) => json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
    /// <summary>读取整数请求字段。</summary>
    private static int Number(JsonElement json, string key) => json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : -1;
    /// <summary>注册会话及账号管理 API。</summary>
    public static IEndpointRouteBuilder MapAccessApis(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/access");
        group.AddEndpointFilter(async (invocation, next) => invocation.Arguments.OfType<JsonElement>().Any(x => x.ValueKind != JsonValueKind.Object)
            ? Results.Problem(statusCode: 400, detail: "请求必须为 JSON 对象。") : await next(invocation));
        group.MapGet("/session", async (AccessDirectoryService service, AdministratorBootstrapKeyStore bootstrapKeys, HttpContext context, CancellationToken ct) => {
            var (directory, _) = await service.ReadAsync(ct);
            if (directory.HasManagedUsers) bootstrapKeys.Retire();
            var localBootstrap = !directory.HasManagedUsers && !service.BootstrapAvailable;
            var localKey = localBootstrap && DatabaseStartupState.IsLocal(context) ? bootstrapKeys.GetOrCreate() : null;
            var user = directory.Users.SingleOrDefault(x => x.Id == context.User.FindFirstValue(ClaimTypes.NameIdentifier));
            string? avatarUrl = null;
            if (user is not null) { var (profile, revision) = await service.ReadProfileAsync(user.Id, ct); avatarUrl = profile.AvatarUrl(revision); }
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { configured = directory.HasManagedUsers, bootstrapAvailable = !directory.HasManagedUsers && (service.BootstrapAvailable || localKey is not null),
                bootstrapLocalOnly = localBootstrap, bootstrapKeyPath = localBootstrap && DatabaseStartupState.IsLocal(context) ? bootstrapKeys.KeyPath : null,
                authenticated = context.User.Identity?.IsAuthenticated == true,
                isSuperAdministrator = AccessDirectoryService.IsSuperAdministrator(context.User), enforceAuthorization = service.EnforceAuthorization,
                name = context.User.Identity?.Name, avatarUrl, permissions = context.User.FindAll("permission").Select(x => x.Value).ToArray() });
        }).WithSummary("读取当前登录状态与访问权限")
            .WithDescription("返回系统是否需要创建管理员、当前会话身份及权限，用于登录入口和页面访问控制；不返回密码或初始化密钥。");
        group.MapPost("/bootstrap", async (JsonElement body, AccessDirectoryService service, AdministratorBootstrapKeyStore bootstrapKeys, HttpContext context, CancellationToken ct) => {
            var (directory, revision) = await service.ReadAsync(ct);
            if (directory.HasManagedUsers) { bootstrapKeys.Retire(); return Results.Problem(statusCode: 409, detail: "系统已有成员，请直接登录。"); }
            if (!service.BootstrapAvailable && !DatabaseStartupState.IsLocalWrite(context))
                return Results.Problem(statusCode: 403, detail: "未配置部署初始化密钥，请在服务器本机使用管理员初始化密钥文件完成创建。");
            var localKey = service.BootstrapAvailable ? null : bootstrapKeys.GetOrCreate();
            if (!service.MatchesSecret(Text(body, "bootstrapKey"), "Access:BootstrapKey", localKey)) return Results.Problem(statusCode: 403, detail: "管理员初始化密钥无效。该密钥与数据库配置访问码不同，请按本页提示获取。");
            var account = Text(body, "username").Trim(); var name = Text(body, "name").Trim(); var password = Text(body, "password");
            if (BuiltInSuperUser.IsReservedAccount(account)) return Results.Problem(statusCode: 400, detail: "hisoka 为内置超级用户的保留账号名，请使用其他账号名创建管理员。");
            if (!AccessDirectoryService.ValidUser(account, name, password, true)) return Results.Problem(statusCode: 400, detail: "账号需为 3~64 位字母、数字或 _.-；姓名不能为空；密码长度为 12~128。");
            var user = AccessDirectoryService.CreateUser(account, name, AccessDirectoryService.SuperAdministratorRoleId, password);
            if (!await service.SaveAsync(directory with { Initialized = true, Users = [.. directory.Users.Where(BuiltInSuperUser.Is), user] }, revision, ct)) return Results.Problem(statusCode: 409, detail: "初始化存在并发冲突，请登录或重试。");
            bootstrapKeys.Retire();
            await context.SignInAsync("SortingCookie", AccessDirectoryService.Principal(user, directory.Roles[0]));
            return Results.Ok(new { name = user.Name });
        }).RequireRateLimiting("account-login").WithSummary("首次创建管理员并登录")
            .WithDescription("仅在不存在普通成员时，校验部署初始化密钥后创建超级管理员并建立登录会话；内置保留账号不能用于初始化，并发初始化返回冲突。");
        group.MapPost("/login", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            var (directory, revision) = await service.ReadAsync(ct);
            var account = Text(body, "username").Trim(); var password = Text(body, "password");
            var user = directory.Users.SingleOrDefault(x => x.Account.Equals(account, StringComparison.OrdinalIgnoreCase));
            if (user is null || !AccessDirectoryService.VerifyPassword(user, password)) return Results.Problem(statusCode: 401, detail: "账号或密码不正确，请重试。");
            var role = directory.Roles.Single(x => x.Id == user.RoleId);
            var updated = user with { LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) };
            await service.SaveAsync(directory with { Users = directory.Users.Select(x => x.Id == user.Id ? updated : x).ToArray() }, revision, ct);
            await context.SignInAsync("SortingCookie", AccessDirectoryService.Principal(user, role), new AuthenticationProperties {
                IsPersistent = body.TryGetProperty("remember", out var remember) && remember.ValueKind == JsonValueKind.True
            });
            return Results.Ok(new { name = user.Name });
        }).RequireRateLimiting("account-login").WithSummary("账号密码登录")
            .WithDescription("校验启用账号的登录密码，更新最近登录时间并建立认证会话；账号不区分大小写，可选择保持登录，错误凭据或禁用账号无法登录。");
        group.MapPost("/logout", async (HttpContext context) => { await context.SignOutAsync("SortingCookie"); return Results.NoContent(); })
            .WithSummary("退出当前登录会话").WithDescription("清除当前用户的认证 Cookie，结束本次登录会话并返回空响应。");
        group.MapGet("", async (AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (!IsAdmin(context)) return Results.Problem(statusCode: 403, detail: "请以具有账号管理权限的账号登录。");
            var (directory, revision) = await service.ReadAsync(ct);
            return Results.Ok(AccessDirectoryService.PublicDirectory(directory, revision));
        }).WithSummary("读取用户与角色目录")
            .WithDescription("具有账号管理权限的用户可读取成员、角色、权限及目录版本，用于账号管理；隐藏内置超级用户，不返回密码摘要。");
        group.MapPost("/roles", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (!IsAdmin(context)) return Results.Problem(statusCode: 403, detail: "需要账号管理权限。");
            var (directory, revision) = await service.ReadAsync(ct);
            if (Number(body, "expectedRevision") != revision) return Results.Problem(statusCode: 409, detail: "账号目录已变更，请刷新重试。");
            var id = Number(body, "id"); var existing = directory.Roles.SingleOrDefault(x => x.Id == id);
            if (body.TryGetProperty("id", out _) && existing is null) return Results.Problem(statusCode: 404, detail: "角色不存在。");
            if (existing?.BuiltIn == true) return Results.Problem(statusCode: 400, detail: "内置管理员角色不可修改。");
            var name = Text(body, "name").Trim(); var description = Text(body, "description");
            if (name.Length is < 1 or > 100 || description.Length > 512 || existing is null && directory.Roles.Length >= 100 || !body.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Array || permissions.GetArrayLength() > 100)
                return Results.Problem(statusCode: 400, detail: "角色名称、说明或权限无效。");
            if (permissions.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) return Results.Problem(statusCode: 400, detail: "权限代码必须为字符串。");
            var codes = permissions.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Distinct().ToArray();
            if (codes.Any(x => !AccessDirectoryService.PermissionCodes.Contains(x))) return Results.Problem(statusCode: 400, detail: "存在未知权限代码。");
            var role = new AccessRole { Id = existing?.Id ?? directory.Roles.Max(x => x.Id) + 1, Name = name, Description = description, Permissions = codes, Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) };
            var updated = directory with { Roles = existing is null ? [.. directory.Roles, role] : directory.Roles.Select(x => x.Id == role.Id ? role : x).ToArray() };
            return await service.SaveAsync(updated, revision, ct) ? Results.Ok(AccessDirectoryService.PublicDirectory(updated, revision + 1)) : Results.Problem(statusCode: 409, detail: "并发修改，请刷新重试。");
        }).WithSummary("创建或修改角色权限")
            .WithDescription("具有账号管理权限的用户可按目录版本新建或更新自定义角色及权限；禁止修改内置角色，版本冲突时保留已有配置。");
        group.MapPost("/users", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (!IsAdmin(context)) return Results.Problem(statusCode: 403, detail: "需要账号管理权限。");
            var (directory, revision) = await service.ReadAsync(ct);
            if (Number(body, "expectedRevision") != revision) return Results.Problem(statusCode: 409, detail: "账号目录已变更，请刷新重试。");
            var id = Text(body, "id"); var existing = directory.Users.SingleOrDefault(x => x.Id == id);
            if (body.TryGetProperty("id", out _) && existing is null) return Results.Problem(statusCode: 404, detail: "账号不存在。");
            if (existing is not null && BuiltInSuperUser.Is(existing)) return Results.Problem(statusCode: 400, detail: "内置超级用户由程序固定，不能修改账号、密码、角色或启用状态。");
            var account = Text(body, "account").Trim(); var name = Text(body, "name").Trim(); var password = Text(body, "password"); var roleId = Number(body, "roleId");
            if (!AccessDirectoryService.IsSuperAdministrator(context.User)
                && (roleId == AccessDirectoryService.SuperAdministratorRoleId || existing?.RoleId == AccessDirectoryService.SuperAdministratorRoleId))
                return Results.Problem(statusCode: 403, detail: "仅超级管理员或内置超级用户可以创建或维护超级管理员账号。");
            if (BuiltInSuperUser.IsReservedAccount(account)) return Results.Problem(statusCode: 400, detail: "hisoka 为内置超级用户的保留账号名，不能用于创建或重命名其他用户。");
            var enabled = !body.TryGetProperty("enabled", out var flag) || flag.ValueKind == JsonValueKind.True;
            if (body.TryGetProperty("enabled", out flag) && flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return Results.Problem(statusCode: 400, detail: "启用状态必须为布尔值。");
            if (!AccessDirectoryService.ValidUser(account, name, password, existing is null) || !directory.Roles.Any(x => x.Id == roleId) || existing is null && directory.Users.Length >= 500 || directory.Users.Any(x => x.Id != id && x.Account.Equals(account, StringComparison.OrdinalIgnoreCase)))
                return Results.Problem(statusCode: 400, detail: "账号输入、角色或密码无效；账号不可重复，密码长度为 12~128。");
            var user = string.IsNullOrEmpty(password) && existing is not null ? existing with { Account = account, Name = name, RoleId = roleId, Enabled = enabled } : AccessDirectoryService.CreateUser(account, name, roleId, password) with { Id = existing?.Id ?? Guid.NewGuid().ToString("N"), Enabled = enabled };
            if (existing is not null && (existing.Enabled != enabled || existing.RoleId != roleId)) user = user with { SecurityStamp = Guid.NewGuid().ToString("N") };
            var updated = directory with { Users = existing is null ? [.. directory.Users, user] : directory.Users.Select(x => x.Id == existing.Id ? user : x).ToArray() };
            if (!updated.Users.Any(x => x.Enabled && updated.Roles.Any(r => r.Id == x.RoleId && r.BuiltIn))) return Results.Problem(statusCode: 400, detail: "至少保留一个启用的超级管理员。");
            return await service.SaveAsync(updated, revision, ct) ? Results.Ok(AccessDirectoryService.PublicDirectory(updated, revision + 1)) : Results.Problem(statusCode: 409, detail: "并发修改，请刷新重试。");
        }).WithSummary("创建或修改用户账号")
            .WithDescription("具有账号管理权限的用户可按目录版本维护账号、角色、密码及启用状态；超级管理员成员仅允许超级管理员维护，内置保留账号不可创建或修改。");
        routes.MapProfileApis();
        return routes;
    }
}
