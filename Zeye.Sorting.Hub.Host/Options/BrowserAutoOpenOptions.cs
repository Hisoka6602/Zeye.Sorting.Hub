namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// 开发期浏览器自动打开配置。
/// </summary>
public sealed class BrowserAutoOpenOptions {
    /// <summary>
    /// 是否启用自动打开浏览器。
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 自动打开地址；为空时会按监听地址与 Swagger 前缀自动拼装。
    /// </summary>
    public string? Url { get; init; }
}
