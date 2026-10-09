namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>只允许重启已经保存且版本未变化的 Host 配置。</summary>
/// <param name="Revision">当前已保存的配置版本，必须与服务器最新版本一致。</param>
public sealed record HostRestartRequest(string Revision);
