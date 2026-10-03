using System.Text.Json.Serialization;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>账号目录文档，账号与角色通过同一个版本原子更新。</summary>
public sealed record AccessDirectory {
    /// <summary>历史上已完成首次管理员创建；保留内置身份的启用状态。</summary>
    public bool Initialized { get; init; }
    /// <summary>当前是否存在非内置成员；不存在时重新提供管理员创建入口。</summary>
    [JsonIgnore]
    public bool HasManagedUsers => Users.Any(x => !BuiltInSuperUser.Is(x));
    /// <summary>账号列表。</summary>
    public AccessUser[] Users { get; init; } = [];
    /// <summary>角色列表。</summary>
    public AccessRole[] Roles { get; init; } = [];
}
