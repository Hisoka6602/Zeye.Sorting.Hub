using System.Text.Json.Nodes;
namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>仅供超级管理员读取的保存原值、生效原值、版本及需要重启的参数。</summary>
public sealed record RuntimeConfigurationState(JsonObject Configuration, JsonObject EffectiveConfiguration, string Revision, string StoragePath,
    string[] OverriddenKeys, string[] RestartRequiredKeys, string[] HotReloadKeys, string? LastReloadError);
