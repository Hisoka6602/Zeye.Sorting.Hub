namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 捕获日志桩使用的空作用域。
/// </summary>
internal sealed class CaptureNullScope : IDisposable {
    /// <summary>
    /// 共享单例空作用域，避免 BeginScope 每次分配对象并降低高频日志测试开销。
    /// </summary>
    public static readonly CaptureNullScope Instance = new();

    /// <summary>
    /// 空实现，无需释放资源。
    /// </summary>
    public void Dispose() { }
}
