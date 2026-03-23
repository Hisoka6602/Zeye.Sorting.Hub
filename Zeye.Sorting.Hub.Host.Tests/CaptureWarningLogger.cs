using Microsoft.Extensions.Logging;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Warning 日志捕获器测试桩。
/// </summary>
internal sealed class CaptureWarningLogger : ILogger {
    /// <summary>
    /// 收集 Warning 及以上级别日志消息文本，用于断言回退路径是否正确产生日志。
    /// </summary>
    public readonly List<string> WarningMessages = [];

    /// <summary>
    /// 返回空作用域单例，避免作用域对象重复分配。
    /// </summary>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => CaptureNullScope.Instance;

    /// <summary>
    /// 对测试场景始终返回 true，确保各级别日志都能进入 Log 方法供断言。
    /// </summary>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>
    /// 仅收集 Warning 及以上级别日志消息，便于断言版本解析回退路径是否产生日志。
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (logLevel < LogLevel.Warning) {
            return;
        }

        WarningMessages.Add(formatter(state, exception));
    }
}
