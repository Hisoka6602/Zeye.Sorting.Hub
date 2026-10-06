using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Zeye.Sorting.Hub.Host.Logging;
using FrameworkLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>使用生产 NLog 配置和独立日志工厂验证异常落盘，不修改其他测试或宿主日志状态。</summary>
public sealed class ExceptionLoggingTests : IDisposable {
    /// <summary>每个测试实例独占的日志目录。</summary>
    private readonly string _directory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "zeye-exception-logging-tests", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>普通规则过滤掉的低级别异常与数据库 final 规则中的异常也必须写入独立文件。</summary>
    [Theory]
    [InlineData("Application.Service", "Trace")]
    [InlineData("Application.Service", "Debug")]
    [InlineData("Application.Service", "Info")]
    [InlineData("Application.Service", "Error")]
    [InlineData("Application.Service", "Fatal")]
    [InlineData("Zeye.Sorting.Hub.Infrastructure.Persistence.Database", "Debug")]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command", "Error")]
    [InlineData("Zeye.Sorting.Hub.Host.HostedServices.DatabaseInitializerHostedService", "Error")]
    public void ExceptionLogsPersistAcrossLevelsAndCategories(string category, string level) {
        using var factory = CreateFactory();
        var entry = new LogEventInfo(NLog.LogLevel.FromString(level), category, "exception-context") { Exception = CaptureFailure(factory) };
        entry.Properties["TraceId"] = "trace-logging-test";
        entry.Properties["ConnectionId"] = "connection-logging-test";
        factory.GetLogger(category).Log(entry);
        factory.Flush(TimeSpan.FromSeconds(10));
        var content = ReadExceptionLogs();
        Assert.Contains("exception-context", content);
        Assert.Contains("outer-logging-failure", content);
        Assert.Contains("inner-logging-failure", content);
        Assert.Contains(nameof(CaptureFailure), content);
        Assert.Contains("trace-logging-test", content);
        Assert.Contains("connection-logging-test", content);
    }

    /// <summary>不携带异常的普通 Debug 信息不能污染异常文件，错误级消息即使无异常对象也必须落盘。</summary>
    [Fact]
    public void PlainDebugIsFilteredButErrorWithoutExceptionPersists() {
        using var factory = CreateFactory();
        var logger = factory.GetLogger("Application.Service");
        logger.Debug("plain-debug-marker");
        logger.Error("error-without-exception-marker");
        factory.Flush(TimeSpan.FromSeconds(10));
        Assert.DoesNotContain("plain-debug-marker", ReadExceptionLogs());
        Assert.Contains("error-without-exception-marker", ReadExceptionLogs());
    }

    /// <summary>把异步队列压缩到两条后制造突发日志，校验无丢弃且每条记录都能读取。</summary>
    [Fact]
    public void AsyncQueueGrowthPersistsEveryBurstException() {
        using var factory = CreateFactory(document => {
            var wrapper = document.Descendants().Single(element => element.Name.LocalName == "default-wrapper");
            wrapper.SetAttributeValue("queueLimit", 2);
            wrapper.SetAttributeValue("timeToSleepBetweenBatches", 1000);
        });
        var wrapper = Assert.IsType<AsyncTargetWrapper>(factory.Configuration!.FindTargetByName("exception-file"));
        Assert.Equal(AsyncTargetWrapperOverflowAction.Grow, wrapper.OverflowAction);
        var dropped = 0;
        wrapper.LogEventDropped += (_, _) => Interlocked.Increment(ref dropped);
        var logger = factory.GetLogger("Application.Burst");
        Parallel.For(0, 2500, index => logger.Debug(new IOException("burst-failure"), "BURST-{0:D4}", index));
        factory.Flush(TimeSpan.FromSeconds(10));
        var markers = Regex.Matches(ReadExceptionLogs(), @"BURST-\d{4}").Select(match => match.Value).ToArray();
        Assert.Equal(0, dropped);
        Assert.Equal(2500, markers.Length);
        Assert.Equal(2500, markers.Distinct().Count());
    }

    /// <summary>默认文件目标维持十 MiB 轮转，在缩小的测试阈值下验证同日多个归档不覆盖异常。</summary>
    [Fact]
    public void RollingArchivesPreserveAllExceptionEntries() {
        using var factory = CreateFactory();
        var wrapper = Assert.IsType<AsyncTargetWrapper>(factory.Configuration!.FindTargetByName("exception-file"));
        var file = Assert.IsType<FileTarget>(wrapper.WrappedTarget);
        Assert.Equal(10485760, file.ArchiveAboveSize);
        Assert.Equal(FileArchivePeriod.Day, file.ArchiveEvery);
        file.ArchiveAboveSize = 2048;
        file.MaxArchiveFiles = 200;
        wrapper.BatchSize = 1;
        var logger = factory.GetLogger("Application.Rotation");
        for (var index = 0; index < 60; index++) {
            logger.Error(new InvalidOperationException(new string('x', 300)), "ROTATION-{0:D3}", index);
            factory.Flush(TimeSpan.FromSeconds(10));
        }
        Assert.True(Directory.GetFiles(_directory, "exceptions*.log", SearchOption.AllDirectories).Length > 1);
        var entries = Regex.Matches(ReadExceptionLogs(), @"ROTATION-\d{3}").Select(match => match.Value).ToArray();
        Assert.Equal(60, entries.Length);
        Assert.Equal(60, entries.Distinct().Count());
    }

    /// <summary>关闭框架常规日志不能隐藏故障；成功 SQL 仍遵循现有关闭策略。</summary>
    [Fact]
    public void FrameworkErrorSurvivesDisabledLoggingWithoutEnablingSqlInformation() {
        using var factory = CreateFactory();
        using var logging = LoggerFactory.Create(builder => {
            builder.SetMinimumLevel(FrameworkLogLevel.None);
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", FrameworkLogLevel.Warning);
            builder.AddProvider(new NLogLoggerProvider(new NLogProviderOptions { RemoveLoggerFactoryFilter = false }, factory));
            builder.Services.AddSingleton<IPostConfigureOptions<LoggerFilterOptions>, ExceptionLoggingFilter>();
        });
        var framework = logging.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host");
        Assert.False(framework.IsEnabled(FrameworkLogLevel.Information));
        Assert.True(framework.IsEnabled(FrameworkLogLevel.Error));
        framework.LogError(new IOException("framework-error-marker"), "后台服务失败。");
        var database = logging.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        Assert.False(database.IsEnabled(FrameworkLogLevel.Information));
        database.LogInformation("successful-sql-marker");
        factory.Flush(TimeSpan.FromSeconds(10));
        Assert.Contains("framework-error-marker", ReadExceptionLogs());
        Assert.DoesNotContain("successful-sql-marker", ReadAllLogs());
    }

    /// <summary>进程异常与未观察任务保留原始堆栈，生命周期结束会写完最后一条入队日志。</summary>
    [Fact]
    public void ProcessExceptionHandlersAndShutdownFlushPersistFinalRecords() {
        using var factory = CreateFactory();
        using (var lifetime = new ExceptionLoggingLifetime(factory)) {
            lifetime.OnUnhandledException(null, new UnhandledExceptionEventArgs(CaptureFailure(factory), true));
            var unobserved = new UnobservedTaskExceptionEventArgs(new AggregateException(new IOException("task-failure-marker")));
            lifetime.OnUnobservedTaskException(null, unobserved);
            Assert.False(unobserved.Observed);
            factory.GetLogger("Application.Shutdown").Error("last-before-shutdown-marker");
        }
        Assert.Contains("outer-logging-failure", ReadExceptionLogs());
        Assert.Contains("task-failure-marker", ReadExceptionLogs());
        Assert.Contains("last-before-shutdown-marker", ReadExceptionLogs());
        Assert.Empty(Directory.GetFiles(_directory, ".log-write-check-*.tmp"));
    }

    /// <summary>日志目录不可写或未配置异常目标时拒绝启动，避免运行后静默丢失异常。</summary>
    [Fact]
    public void StartupRejectsInvalidLogDestination() {
        using var factory = CreateFactory();
        var blocker = Path.Combine(_directory, "file-instead-of-directory");
        File.WriteAllText(blocker, "blocked");
        var wrapper = Assert.IsType<AsyncTargetWrapper>(factory.Configuration!.FindTargetByName("exception-file"));
        Assert.IsType<FileTarget>(wrapper.WrappedTarget).FileName = Path.Combine(blocker, "exceptions.log");
        Assert.Throws<IOException>(() => ExceptionLoggingLifetime.VerifyFileTargets(factory));
        using var empty = new LogFactory { Configuration = new LoggingConfiguration() };
        Assert.Throws<InvalidOperationException>(() => ExceptionLoggingLifetime.VerifyFileTargets(empty));
        var console = new AsyncTargetWrapper(new ConsoleTarget()) { Name = "exception-file" };
        empty.Configuration.AddTarget(console);
        Assert.Throws<InvalidOperationException>(() => ExceptionLoggingLifetime.VerifyFileTargets(empty));
    }

    /// <summary>在真实长轮询连接中验证普通调用及流迭代异常，客户端不暴露完整内部原因。</summary>
    [Fact]
    public async Task SignalRInvocationAndStreamingFailuresPersistWithContext() {
        using var factory = CreateFactory();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(FrameworkLogLevel.None);
        builder.Logging.AddProvider(new NLogLoggerProvider(new NLogProviderOptions { RemoveLoggerFactoryFilter = false }, factory));
        builder.Services.AddSingleton<IPostConfigureOptions<LoggerFilterOptions>, ExceptionLoggingFilter>();
        builder.Services.AddSingleton(factory);
        builder.Services.AddSingleton<ExceptionLoggingHubFilter>();
        builder.Services.AddSignalR(options => options.AddFilter<ExceptionLoggingHubFilter>());
        await using var app = builder.Build();
        app.MapHub<ExceptionLoggingTestHub>("/logging-test");
        await app.StartAsync();
        await using var connection = new HubConnectionBuilder().WithUrl("http://localhost/logging-test", options => {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
        }).Build();
        await connection.StartAsync();
        var connectionId = connection.ConnectionId;
        var failure = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Fail"));
        Assert.DoesNotContain("hub-inner-failure", failure.Message);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = connection.StreamAsync<int>("StreamFailure", cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(1, stream.Current);
        await Assert.ThrowsAsync<HubException>(async () => { await stream.MoveNextAsync(); });
        await connection.StopAsync();
        factory.Flush(TimeSpan.FromSeconds(10));
        var content = ReadExceptionLogs();
        Assert.Contains("hub-original-failure", content);
        Assert.Contains("hub-inner-failure", content);
        Assert.Contains("stream-original-failure", content);
        Assert.False(string.IsNullOrWhiteSpace(connectionId));
        Assert.Contains($"ConnectionId={connectionId}", content);
        Assert.Contains("Method=Fail", content);
    }

    /// <summary>读取实际发布配置，只把输出目录和控制台目标替换成隔离测试设置。</summary>
    private LogFactory CreateFactory(Action<XDocument>? configure = null) {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "nlog.config"));
        document.Root!.SetAttributeValue("autoReload", false);
        document.Root.SetAttributeValue("internalLogLevel", "Off");
        document.Descendants().Single(element => element.Name.LocalName == "variable" && (string?)element.Attribute("name") == "logDirectory")
            .SetAttributeValue("value", _directory.Replace('\\', '/'));
        foreach (var rule in document.Descendants().Where(element => element.Name.LocalName == "logger")) {
            var target = rule.Attribute("writeTo");
            if (target is not null) target.Value = target.Value.Replace("console,", string.Empty, StringComparison.Ordinal);
        }
        configure?.Invoke(document);
        var factory = new LogFactory { ThrowConfigExceptions = true };
        using var reader = new StringReader(document.ToString());
        factory.Configuration = new XmlLoggingConfiguration(reader, Path.Combine(_directory, "nlog.config"), factory);
        return factory;
    }

    /// <summary>制造真实抛出过的异常，确保断言覆盖完整堆栈而非仅错误文本。</summary>
    private static Exception CaptureFailure(LogFactory factory) {
        try { throw new InvalidOperationException("outer-logging-failure", new IOException("inner-logging-failure")); }
        catch (InvalidOperationException exception) {
            var logger = factory.GetLogger(nameof(ExceptionLoggingTests));
            logger.Trace(exception, "生成异常日志回归样本。");
            return exception;
        }
    }

    /// <summary>读取当前异常日志及所有轮转归档。</summary>
    private string ReadExceptionLogs() => ReadLogs("exceptions*.log");

    /// <summary>读取隔离目录中的全部日志，用于确认成功 SQL 未被意外开启。</summary>
    private string ReadAllLogs() => ReadLogs("*.log");

    /// <summary>允许 NLog 保持写句柄，同时在 Windows 和 Linux 读取已经刷新完成的文件。</summary>
    private string ReadLogs(string pattern) => string.Join('\n', Directory.GetFiles(_directory, pattern, SearchOption.AllDirectories).Select(path => {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }));

    /// <summary>仅删除该测试实例创建的唯一目录。</summary>
    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
