namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 通用测试日志空作用域。
/// </summary>
internal sealed class NullScope : IDisposable {
    /// <summary>
    /// 共享单例空作用域，避免 TestLogger 在高频 BeginScope 调用时产生额外分配。
    /// </summary>
    public static readonly NullScope Instance = new();

    /// <summary>
    /// 空实现，无需释放资源。
    /// </summary>
    public void Dispose() { }
}
