namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>仅用于服务端持久化的账号，口令散列不出现在接口响应。</summary>
public sealed record AccessUser {
    /// <summary>程序固定的账号，不允许通过用户管理修改身份、凭据、角色或启用状态。</summary>
    public bool BuiltIn { get; init; }
    /// <summary>账号编号。</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>登录名。</summary>
    public string Account { get; init; } = string.Empty;
    /// <summary>显示名称。</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>角色编号。</summary>
    public long RoleId { get; init; }
    /// <summary>带随机盐的口令散列。</summary>
    public string PasswordHash { get; init; } = string.Empty;
    /// <summary>启用状态。</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>会话失效标识。</summary>
    public string SecurityStamp { get; init; } = string.Empty;
    /// <summary>最近登录的本地时间。</summary>
    public string? LastLogin { get; init; }
}
