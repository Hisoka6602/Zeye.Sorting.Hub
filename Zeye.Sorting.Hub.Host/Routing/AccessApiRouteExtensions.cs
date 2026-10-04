using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Zeye.Sorting.Hub.Host.Queries;
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
        group.MapGet("/session", async (AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            var (directory, _) = await service.ReadAsync(ct);
            var user = directory.Users.SingleOrDefault(x => x.Id == context.User.FindFirstValue(ClaimTypes.NameIdentifier));
            string? avatarUrl = null;
            if (user is not null) { var (profile, revision) = await service.ReadProfileAsync(user.Id, ct); avatarUrl = profile.AvatarUrl(revision); }
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { configured = directory.HasManagedUsers, bootstrapAvailable = service.BootstrapAvailable, authenticated = context.User.Identity?.IsAuthenticated == true,
                isSuperAdministrator = AccessDirectoryService.IsSuperAdministrator(context.User), enforceAuthorization = service.EnforceAuthorization,
                name = context.User.Identity?.Name, avatarUrl, permissions = context.User.FindAll("permission").Select(x => x.Value).ToArray() });
        });
        group.MapPost("/bootstrap", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (!service.MatchesSecret(Text(body, "bootstrapKey"), "Access:BootstrapKey")) return Results.Problem(statusCode: 403, detail: "初始化密钥无效或未配置。");
            var (directory, revision) = await service.ReadAsync(ct);
            if (directory.HasManagedUsers) return Results.Problem(statusCode: 409, detail: "系统已有成员，请直接登录。");
            var account = Text(body, "username").Trim(); var name = Text(body, "name").Trim(); var password = Text(body, "password");
            if (BuiltInSuperUser.IsReservedAccount(account)) return Results.Problem(statusCode: 400, detail: "hisoka 为内置超级用户的保留账号名，请使用其他账号名创建管理员。");
            if (!AccessDirectoryService.ValidUser(account, name, password, true)) return Results.Problem(statusCode: 400, detail: "账号需为 3~64 位字母、数字或 _.-；姓名不能为空；密码长度为 12~128。");
            var user = AccessDirectoryService.CreateUser(account, name, AccessDirectoryService.SuperAdministratorRoleId, password);
            if (!await service.SaveAsync(directory with { Initialized = true, Users = [.. directory.Users.Where(BuiltInSuperUser.Is), user] }, revision, ct)) return Results.Problem(statusCode: 409, detail: "初始化存在并发冲突，请登录或重试。");
            await context.SignInAsync("SortingCookie", AccessDirectoryService.Principal(user, directory.Roles[0]));
            return Results.Ok(new { name = user.Name });
        }).RequireRateLimiting("account-login");
        group.MapPost("/login", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            var (directory, revision) = await service.ReadAsync(ct);
            var account = Text(body, "username").Trim(); var password = Text(body, "password");
            var user = directory.Users.SingleOrDefault(x => x.Account.Equals(account, StringComparison.OrdinalIgnoreCase));
            if (user is null || !AccessDirectoryService.VerifyPassword(user, password)) return Results.Problem(statusCode: 401, detail: "账号或密码不正确，请重试。");
            var role = directory.Roles.Single(x => x.Id == user.RoleId);
            var updated = user with { LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            await service.SaveAsync(directory with { Users = directory.Users.Select(x => x.Id == user.Id ? updated : x).ToArray() }, revision, ct);
            await context.SignInAsync("SortingCookie", AccessDirectoryService.Principal(user, role), new AuthenticationProperties {
                IsPersistent = body.TryGetProperty("remember", out var remember) && remember.ValueKind == JsonValueKind.True
            });
            return Results.Ok(new { name = user.Name });
        }).RequireRateLimiting("account-login");
        group.MapPost("/logout", async (HttpContext context) => { await context.SignOutAsync("SortingCookie"); return Results.NoContent(); });
        group.MapGet("", async (AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (!IsAdmin(context)) return Results.Problem(statusCode: 403, detail: "请以具有账号管理权限的账号登录。");
            var (directory, revision) = await service.ReadAsync(ct);
            return Results.Ok(AccessDirectoryService.PublicDirectory(directory, revision));
        });
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
            var role = new AccessRole { Id = existing?.Id ?? directory.Roles.Max(x => x.Id) + 1, Name = name, Description = description, Permissions = codes, Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
            var updated = directory with { Roles = existing is null ? [.. directory.Roles, role] : directory.Roles.Select(x => x.Id == role.Id ? role : x).ToArray() };
            return await service.SaveAsync(updated, revision, ct) ? Results.Ok(AccessDirectoryService.PublicDirectory(updated, revision + 1)) : Results.Problem(statusCode: 409, detail: "并发修改，请刷新重试。");
        });
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
        });
        routes.MapProfileApis();
        return routes;
    }
}
