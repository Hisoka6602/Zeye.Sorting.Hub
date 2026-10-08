using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Zeye.Sorting.Hub.Host.Logging;

/// <summary>为所有 SignalR 调用和连接生命周期提供统一异常日志，不记录方法参数或凭据。</summary>
public sealed class ExceptionLoggingHubFilter(LogFactory factory) : IHubFilter {
    /// <summary>记录 Hub 调用故障的日志器。</summary>
    private readonly Logger _logger = factory.GetLogger(typeof(ExceptionLoggingHubFilter).FullName!);

    /// <summary>记录 Hub 方法调用异常并原样传播，保持现有客户端错误语义。</summary>
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation,
        Func<HubInvocationContext, ValueTask<object?>> next) {
        try { return await next(invocation); }
        catch (Exception exception) {
            _logger.Log(CreateFailure(exception, invocation.Context, invocation.Hub.GetType().Name, invocation.HubMethodName));
            throw;
        }
    }

    /// <summary>记录连接初始化失败并继续按框架规则拒绝连接。</summary>
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next) {
        try { await next(context); }
        catch (Exception exception) {
            _logger.Log(CreateFailure(exception, context.Context, context.Hub.GetType().Name, nameof(OnConnectedAsync)));
            throw;
        }
    }

    /// <summary>记录断开原因及连接清理失败，正常断开不产生故障日志。</summary>
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next) {
        if (exception is not null) _logger.Log(CreateFailure(exception, context.Context, context.Hub.GetType().Name, nameof(OnDisconnectedAsync)));
        try { await next(context, exception); }
        catch (Exception failure) {
            _logger.Log(CreateFailure(failure, context.Context, context.Hub.GetType().Name, nameof(OnDisconnectedAsync)));
            throw;
        }
    }

    /// <summary>构建调用身份与完整异常堆栈，参数正文由业务审计控制。</summary>
    private LogEventInfo CreateFailure(Exception exception, HubCallerContext caller, string hub, string method) {
        var entry = new LogEventInfo(NLog.LogLevel.Error, _logger.Name,
            $"SignalR 调用失败，Hub={hub}，Method={method}。") { Exception = exception };
        entry.Properties["ConnectionId"] = caller.ConnectionId;
        entry.Properties["Method"] = method;
        try { entry.Properties["TraceId"] = caller.GetHttpContext()?.TraceIdentifier; }
        catch (ObjectDisposedException contextFailure) {
            // 连接异常退出时请求上下文可能已释放，原始调用异常仍必须按原样落盘和传播。
            _logger.Debug(contextFailure, "SignalR 请求上下文已释放，保留连接身份与原始异常。");
        }
        return entry;
    }
}
