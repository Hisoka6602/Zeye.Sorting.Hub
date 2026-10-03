using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.Routing;
/// <summary>登录用户只能读取和维护自己的个人资料。</summary>
public static class ProfileApiRouteExtensions {
    /// <summary>安全的个人资料响应。</summary>
    private static object Snapshot(AccessUser user, AccessRole role, PersonalProfile profile, int revision, int directoryRevision) => new {
        user.Id, user.Account, user.Name, roleName = role.Name, user.LastLogin, profile.Email, profile.Phone, profile.Bio,
        avatarUrl = profile.AvatarUrl(revision), revision, directoryRevision
    };
    /// <summary>读取严格的字符串字段。</summary>
    private static string? Text(JsonElement body, string key) => body.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    /// <summary>当前用户接口独立于管理员账号维护权限。</summary>
    public static IEndpointRouteBuilder MapProfileApis(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/access/profile").RequireAuthorization();
        group.MapGet("", async (AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            var (directory, directoryRevision) = await service.ReadAsync(ct);
            var user = directory.Users.SingleOrDefault(x => x.Id == context.User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Enabled);
            if (user is null) return Results.Unauthorized();
            var (profile, revision) = await service.ReadProfileAsync(user.Id, ct);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(Snapshot(user, directory.Roles.Single(x => x.Id == user.RoleId), profile, revision, directoryRevision));
        });
        group.MapPut("", async (JsonElement body, AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Any(x => x.Name is not ("name" or "email" or "phone" or "bio" or "avatarDataUrl" or "expectedRevision" or "directoryRevision")))
                return Results.Problem(statusCode: 400, detail: "请求包含不支持的个人资料字段。");
            var name = Text(body, "name")?.Trim(); var email = Text(body, "email")?.Trim(); var phone = Text(body, "phone")?.Trim(); var bio = Text(body, "bio")?.Trim();
            if (name is null || name.Length is < 1 or > 100 || email is null || email.Length > 254 || phone is null || phone.Length > 32 || bio is null || bio.Length > 500)
                return Results.Problem(statusCode: 400, detail: "名称需为 1~100 字，邮箱不超过 254 字，电话不超过 32 字，简介不超过 500 字。");
            if (email.Length > 0 && (!MailAddress.TryCreate(email, out var address) || address.Address != email || address.DisplayName.Length > 0))
                return Results.Problem(statusCode: 400, detail: "请输入有效的邮箱地址。");
            if (phone.Length > 0 && !Regex.IsMatch(phone, @"^[+0-9][0-9() .-]{2,31}$", RegexOptions.CultureInvariant))
                return Results.Problem(statusCode: 400, detail: "请输入有效的联系电话。");
            if (!body.TryGetProperty("expectedRevision", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var expectedRevision) || expectedRevision < 0
                || !body.TryGetProperty("directoryRevision", out var directoryVersion) || directoryVersion.ValueKind != JsonValueKind.Number || !directoryVersion.TryGetInt32(out var expectedDirectoryRevision) || expectedDirectoryRevision < 0)
                return Results.Problem(statusCode: 400, detail: "请先读取当前个人资料再保存。");
            var (directory, directoryRevision) = await service.ReadAsync(ct);
            var user = directory.Users.SingleOrDefault(x => x.Id == context.User.FindFirstValue(ClaimTypes.NameIdentifier) && x.Enabled);
            if (user is null) return Results.Unauthorized();
            var (profile, revision) = await service.ReadProfileAsync(user.Id, ct);
            if (revision != expectedRevision || directoryRevision != expectedDirectoryRevision)
                return Results.Problem(statusCode: 409, detail: "资料或账号目录已更新，请重新加载资料后再保存。");
            var avatar = profile.AvatarDataUrl;
            if (body.TryGetProperty("avatarDataUrl", out _)) {
                avatar = Text(body, "avatarDataUrl");
                if (avatar is null || avatar.Length > 0 && !PersonalProfile.TryDecodeAvatar(avatar, out _, out _))
                    return Results.Problem(statusCode: 400, detail: "头像需为有效的 PNG 或 JPEG 图片，保存大小不超过 96 KB，宽高不超过 1024 像素。");
            }
            var updatedProfile = profile with { Email = email, Phone = phone, Bio = bio, AvatarDataUrl = avatar };
            var updatedUser = user with { Name = name };
            var updatedDirectory = directory with { Users = directory.Users.Select(x => x.Id == user.Id ? updatedUser : x).ToArray() };
            if (!await service.SaveProfileAsync(updatedDirectory, directoryRevision, user.Id, updatedProfile, revision, ct))
                return Results.Problem(statusCode: 409, detail: "资料或账号目录已更新，请重新加载资料后再保存。");
            return Results.Ok(Snapshot(updatedUser, directory.Roles.Single(x => x.Id == user.RoleId), updatedProfile, revision + 1, directoryRevision + 1));
        });
        group.MapGet("/avatar", async (AccessDirectoryService service, HttpContext context, CancellationToken ct) => {
            var (profile, _) = await service.ReadProfileAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
            if (!PersonalProfile.TryDecodeAvatar(profile.AvatarDataUrl, out var bytes, out var contentType)) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-cache";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(bytes, contentType);
        });
        return routes;
    }
}
