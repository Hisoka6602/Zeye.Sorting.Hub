using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>程序固定的超级用户定义；首次管理员初始化完成后才启用。</summary>
public static class BuiltInSuperUser {
    /// <summary>不区分大小写的保留账号名。</summary>
    public const string Account = "hisoka";
    /// <summary>独立于既有账号的稳定身份，避免继承冲突账号的会话和个人资料。</summary>
    public const string Id = "builtin-hisoka";
    /// <summary>程序内置的随机盐 PBKDF2 口令散列；不在数据库和接口中保存明文口令。</summary>
    private const string PasswordHash = "AQAAAAIAAYagAAAAEJbsOXB1IzQMbbCbMpWtG92beA1RfvQn/aG8FxgAZLRBXHKQyK0QwoWsgNnsvodgvw==";
    /// <summary>凭据定义变化时撤销旧会话；普通进程重启保留会话。</summary>
    private static readonly string SecurityStamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PasswordHash)));
    /// <summary>使用框架验证固定凭据。</summary>
    private static readonly PasswordHasher<AccessUser> Hasher = new();
    /// <summary>保留名不能用于初始化、创建或重命名普通用户。</summary>
    public static bool IsReservedAccount(string? account) => string.Equals(account?.Trim(), Account, StringComparison.OrdinalIgnoreCase);
    /// <summary>只有程序定义的身份和标记共同匹配时才视为内置用户。</summary>
    public static bool Is(AccessUser user) => user.BuiltIn && user.Id == Id && IsReservedAccount(user.Account);
    /// <summary>恢复固定的身份、角色和凭据，仅保留个人显示名称及最近登录时间。</summary>
    public static AccessUser Restore(AccessUser? existing) => new() {
        Id = Id, Account = Account, Name = string.IsNullOrWhiteSpace(existing?.Name) ? "内置超级用户" : existing.Name,
        RoleId = 1, PasswordHash = PasswordHash, Enabled = true, BuiltIn = true, SecurityStamp = SecurityStamp, LastLogin = existing?.LastLogin
    };
    /// <summary>口令始终与程序定义比较，账号管理和数据库目录中的凭据改动不能替换它。</summary>
    public static bool VerifyPassword(AccessUser user, string password) => Is(user) && user.Enabled && password.Length <= 128
        && Hasher.VerifyHashedPassword(user, PasswordHash, password) != PasswordVerificationResult.Failed;
}
