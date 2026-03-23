using Microsoft.Extensions.DependencyInjection;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 空服务作用域测试桩。
/// </summary>
internal sealed class EmptyServiceScope : IServiceScope {
    /// <summary>
    /// 提供最小 ServiceProvider 实例，满足需要 IServiceScope.ServiceProvider 的测试路径。
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 验证场景：Dispose。
    /// </summary>
    public void Dispose() { }
}
