using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>管理真实账号、密码散列及可撤销的角色权限。</summary>
public sealed class AccessDirectoryService(ManagedDocumentService store, IConfiguration config) {
    /// <summary>服务端权限白名单。</summary>
    public static readonly string[] PermissionCodes = ["parcels.read", "parcels.write", "audit.read", "diagnostics.read", "governance.manage", "rules.manage", "settings.read", "access.manage"];
    /// <summary>口令散列器，默认使用框架的随机盐 PBKDF2 实现。</summary>
    private static readonly PasswordHasher<AccessUser> Hasher = new();
    /// <summary>服务端目录序列化选项。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>读取当前目录和乐观并发版本。</summary>
    public async Task<(AccessDirectory Directory, int Revision)> ReadAsync(CancellationToken ct) {
        for (var attempt = 0; attempt < 5; attempt++) {
            var document = await store.ReadAsync("access-directory", ct);
            if (document is null) return (Normalize(new()), 0);
            var directory = Normalize(JsonSerializer.Deserialize<AccessDirectory>(document.Json, JsonOptions)!);
            var json = JsonSerializer.Serialize(directory, JsonOptions);
            if (json == document.Json) return (directory, document.Revision);
            // 既有目录自动升级；先删除所有保留名冲突，再保存程序身份。并发失败重读，避免覆盖账号修改。
            var saved = await store.WriteAsync("access-directory", json, document.Revision, ct);
            if (saved is not null) return (directory, saved.Revision);
        }
        throw new InvalidOperationException("账号目录存在并发更新，请重试。");
    }
    /// <summary>保持首次初始化状态，并在所有读写入口统一保留程序定义的账号和完整管理员权限。</summary>
    private static AccessDirectory Normalize(AccessDirectory directory) {
        var initialized = directory.Initialized || directory.Users.Any(x => !BuiltInSuperUser.Is(x));
        var administrator = directory.Roles.FirstOrDefault(x => x.Id == 1) ?? new AccessRole { Id = 1, Name = "超级管理员", Description = "管理平台全部已接入功能" };
        var users = directory.Users.Where(x => !BuiltInSuperUser.IsReservedAccount(x.Account) && x.Id != BuiltInSuperUser.Id).ToArray();
        return directory with {
            Initialized = initialized,
            Roles = [administrator with { BuiltIn = true, Permissions = PermissionCodes }, .. directory.Roles.Where(x => x.Id != 1)],
            Users = initialized ? [.. users, BuiltInSuperUser.Restore(directory.Users.FirstOrDefault(BuiltInSuperUser.Is))] : []
        };
    }
    /// <summary>是否启用全平台权限保护，默认由部署明确选择。</summary>
    public bool EnforceAuthorization => config.GetValue("Access:EnforceAuthorization", false);
    /// <summary>初始化密钥存在时才允许建立首个管理员。</summary>
    public bool BootstrapAvailable => !string.IsNullOrWhiteSpace(config["Access:BootstrapKey"]);
    /// <summary>固定时间比较初始化或机器接口密钥。</summary>
    public bool MatchesSecret(string? supplied, string key) {
        var expected = config[key];
        if (string.IsNullOrWhiteSpace(expected) || supplied is null || supplied.Length > 512) return false;
        var actualHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(supplied));
        var expectedHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expected));
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
    /// <summary>验证账号输入边界。</summary>
    public static bool ValidUser(string account, string name, string? password, bool requirePassword) => account.Length is >= 3 and <= 64
        && !BuiltInSuperUser.IsReservedAccount(account)
        && account.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.') && name.Length is > 0 and <= 100 && !string.IsNullOrWhiteSpace(name)
        && (!requirePassword && string.IsNullOrEmpty(password) || password is { Length: >= 12 and <= 128 });
    /// <summary>建立带随机盐散列的新账号。</summary>
    public static AccessUser CreateUser(string account, string name, long roleId, string password) {
        var user = new AccessUser { Id = Guid.NewGuid().ToString("N"), Account = account, Name = name, RoleId = roleId, SecurityStamp = Guid.NewGuid().ToString("N") };
        return user with { PasswordHash = Hasher.HashPassword(user, password) };
    }
    /// <summary>验证口令，停用账号无法登录。</summary>
    public static bool VerifyPassword(AccessUser user, string password) => BuiltInSuperUser.Is(user) ? BuiltInSuperUser.VerifyPassword(user, password)
        : user.Enabled && password.Length <= 128 && Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    /// <summary>保存账号目录，冲突时拒绝覆盖。</summary>
    public async Task<bool> SaveAsync(AccessDirectory directory, int revision, CancellationToken ct) =>
        await store.WriteAsync("access-directory", JsonSerializer.Serialize(Normalize(directory), JsonOptions), revision, ct) is not null;
    /// <summary>读取当前用户的独立资料文档，兼容没有个人资料的既有账号。</summary>
    public async Task<(PersonalProfile Profile, int Revision)> ReadProfileAsync(string userId, CancellationToken ct) {
        var document = await store.ReadAsync(PersonalProfile.Key(userId), ct);
        return document is null ? (new(), 0) : (JsonSerializer.Deserialize<PersonalProfile>(document.Json, JsonOptions)!, document.Revision);
    }
    /// <summary>原子更新显示名称与个人资料，保留权限和当前认证状态。</summary>
    public Task<bool> SaveProfileAsync(AccessDirectory directory, int directoryRevision, string userId, PersonalProfile profile, int profileRevision, CancellationToken ct) =>
        store.WriteBatchAsync([("access-directory", JsonSerializer.Serialize(Normalize(directory), JsonOptions), directoryRevision),
            (PersonalProfile.Key(userId), JsonSerializer.Serialize(profile, JsonOptions), profileRevision)], ct);
    /// <summary>创建包含当前权限的会话主体。</summary>
    public static ClaimsPrincipal Principal(AccessUser user, AccessRole role) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Name), new Claim("security-stamp", user.SecurityStamp),
        .. (BuiltInSuperUser.Is(user) ? PermissionCodes : role.Permissions).Select(x => new Claim("permission", x))
    ], "SortingCookie"));
    /// <summary>公开普通成员和角色人数，隐藏内置身份、密码散列及安全标记。</summary>
    public static object PublicDirectory(AccessDirectory directory, int revision) {
        var users = directory.Users.Where(x => !BuiltInSuperUser.Is(x)).ToArray();
        return new {
            configured = directory.HasManagedUsers, revision,
            permissions = PermissionCodes.Select(x => new { code = x, label = x switch {
                "parcels.read" => "查看包裹", "parcels.write" => "维护包裹", "audit.read" => "查看请求审计", "diagnostics.read" => "查看诊断",
                "governance.manage" => "管理数据治理", "rules.manage" => "管理规则", "settings.read" => "查看系统配置", _ => "管理账号与权限"
            } }),
            roles = directory.Roles.Select(x => new { x.Id, x.Name, x.Description, x.Permissions, x.BuiltIn, x.Modified, members = users.Count(u => u.RoleId == x.Id) }),
            users = users.Select(x => new { x.Id, x.Account, x.Name, x.RoleId, x.Enabled, x.LastLogin, x.BuiltIn })
        };
    }
}
