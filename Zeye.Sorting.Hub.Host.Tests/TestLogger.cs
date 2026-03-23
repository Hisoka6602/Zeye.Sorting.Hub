using Microsoft.Extensions.Logging;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 通用泛型日志测试桩。
/// </summary>
/// <typeparam name="T">日志分类类型。</typeparam>
internal sealed class TestLogger<T> : ILogger<T> {
    /// <summary>
    /// 收集全部级别日志消息文本，用于断言守卫、审计与回退路径的日志输出内容。
    /// </summary>
    public readonly List<string> Messages = [];

    /// <summary>
    /// 返回空作用域单例，确保 BeginScope 在测试中低开销可重入。
    /// </summary>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <summary>
    /// 验证场景：IsEnabled。
    /// </summary>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>
    /// 验证场景：Log。
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        Messages.Add(formatter(state, exception));
    }
}
