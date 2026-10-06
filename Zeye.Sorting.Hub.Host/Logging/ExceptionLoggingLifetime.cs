using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace Zeye.Sorting.Hub.Host.Logging;

/// <summary>校验落盘目录并记录进程级异常，退出前刷新异步日志。</summary>
public sealed class ExceptionLoggingLifetime : IDisposable {
    /// <summary>当前宿主使用的 NLog 工厂。</summary>
    private readonly LogFactory _factory;
    /// <summary>进程级异常日志器。</summary>
    private readonly Logger _logger;
    /// <summary>确保事件退订与退出刷新只执行一次。</summary>
    private int _disposed;

    /// <summary>启动时校验所有文件目录，并订阅未处理异常及未观察任务异常。</summary>
    public ExceptionLoggingLifetime(LogFactory factory) {
        _factory = factory;
        _logger = factory.GetLogger(typeof(ExceptionLoggingLifetime).FullName!);
        VerifyFileTargets(factory);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>验证实际配置的文件目录可写，不能落盘时拒绝静默启动。</summary>
    internal static void VerifyFileTargets(LogFactory factory) {
        var exceptionTarget = factory.Configuration?.FindTargetByName("exception-file");
        if (exceptionTarget is AsyncTargetWrapper exceptionWrapper) exceptionTarget = exceptionWrapper.WrappedTarget;
        if (exceptionTarget is not FileTarget)
            throw new InvalidOperationException("缺少异常日志文件配置，宿主不能启动。");
        var targets = factory.Configuration?.AllTargets.Select(target =>
            target is AsyncTargetWrapper wrapper ? wrapper.WrappedTarget : target).OfType<FileTarget>().Distinct().ToArray() ?? [];
        var directories = targets.Select(target => Path.GetDirectoryName(target.FileName.Render(
            new LogEventInfo(NLog.LogLevel.Info, nameof(ExceptionLoggingLifetime), string.Empty)))!)
            .Distinct(StringComparer.Ordinal);
        foreach (var directory in directories) {
            Directory.CreateDirectory(directory);
            var probePath = Path.Combine(directory, $".log-write-check-{Guid.NewGuid():N}.tmp");
            using var probe = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1, FileOptions.DeleteOnClose);
            probe.WriteByte(0);
            probe.Flush(flushToDisk: true);
        }
    }

    /// <summary>记录进程退出原因并立即刷新，不吞掉原有未处理异常。</summary>
    internal void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args) {
        _logger.Fatal(args.ExceptionObject as Exception,
            "进程发生未处理异常，IsTerminating={IsTerminating}, FailureType={FailureType}",
            args.IsTerminating, args.ExceptionObject.GetType().FullName);
        _factory.Flush(TimeSpan.FromSeconds(10));
    }

    /// <summary>记录后台任务未观察异常，保留运行时原有异常行为。</summary>
    internal void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args) =>
        _logger.Error(args.Exception, "后台任务发生未观察异常。");

    /// <summary>退订进程事件，并在宿主退出时等待已入队日志写完。</summary>
    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _factory.Flush(TimeSpan.FromSeconds(10));
    }
}
