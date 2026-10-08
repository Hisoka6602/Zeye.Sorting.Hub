using Microsoft.Extensions.Options;
namespace Zeye.Sorting.Hub.Host.Configuration;

/// <summary>为每次读取 Value 的现有组件返回最新 options 快照。</summary>
public sealed class ReloadableOptions<T>(IOptionsMonitor<T> monitor) : IOptions<T> where T : class {
    /// <summary>当前已经通过校验的配置实例。</summary>
    public T Value => monitor.CurrentValue;
}
