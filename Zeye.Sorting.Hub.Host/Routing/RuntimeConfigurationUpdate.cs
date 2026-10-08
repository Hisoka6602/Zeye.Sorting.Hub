using System.Text.Json.Nodes;
namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>携带已读取版本的配置局部更新请求。</summary>
public sealed record RuntimeConfigurationUpdate(string Revision, JsonObject Changes);
