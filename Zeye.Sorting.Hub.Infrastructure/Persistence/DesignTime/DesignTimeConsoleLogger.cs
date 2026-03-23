using Microsoft.Extensions.Logging;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime;

/// <summary>
/// 设计时版本解析日志器：在无 Host 管道时将告警输出到标准错误流。
/// </summary>
internal sealed class DesignTimeConsoleLogger : ILogger {
    /// <summary>
    /// 单例实例。
    /// </summary>
    public static readonly DesignTimeConsoleLogger Instance = new();

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (!IsEnabled(logLevel)) {
            return;
        }

        var message = formatter(state, exception);
        if (exception is null) {
            Console.Error.WriteLine($"[{logLevel}] {message}");
        }
        else {
            Console.Error.WriteLine($"[{logLevel}] {message}{Environment.NewLine}{exception}");
        }
    }
}
