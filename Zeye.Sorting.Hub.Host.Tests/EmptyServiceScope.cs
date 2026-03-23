using Microsoft.Extensions.DependencyInjection;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 空服务作用域测试桩。
/// </summary>
internal sealed class EmptyServiceScope : IServiceScope {
    /// <summary>
    /// 提供最小 ServiceProvider 实例，满足需要 <see cref="IServiceScope.ServiceProvider"/> 的测试路径。
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 释放内部 <see cref="ServiceProvider"/>，避免测试作用域释放后残留可释放资源。
    /// </summary>
    public void Dispose() {
        ((IDisposable)ServiceProvider).Dispose();
    }
}
