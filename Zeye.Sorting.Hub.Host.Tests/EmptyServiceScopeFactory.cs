using Microsoft.Extensions.DependencyInjection;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 空服务作用域工厂测试桩。
/// </summary>
internal sealed class EmptyServiceScopeFactory : IServiceScopeFactory {
    /// <summary>
    /// 验证场景：CreateScope。
    /// </summary>
    public IServiceScope CreateScope() => new EmptyServiceScope();
}
