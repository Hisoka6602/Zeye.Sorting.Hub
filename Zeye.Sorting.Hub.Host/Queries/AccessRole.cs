namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>平台角色及服务端权限。</summary>
public sealed record AccessRole {
    /// <summary>角色编号。</summary>
    public long Id { get; init; }
    /// <summary>角色名称。</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>角色说明。</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>权限代码。</summary>
    public string[] Permissions { get; init; } = [];
    /// <summary>是否为受保护的管理员角色。</summary>
    public bool BuiltIn { get; init; }
    /// <summary>修改时间。</summary>
    public string Modified { get; init; } = string.Empty;
}
