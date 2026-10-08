using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Zeye.Sorting.Hub.Host.Hubs;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.Logging;

/// <summary>补齐机器 SignalR 调用的请求汇总；网页查询由独立端点中间件按每次快照统计。</summary>
public sealed class SlowQueryHubFilter(SlowQueryAutoTuningPipeline pipeline) : IHubFilter {
    /// <summary>以单次调用为单位，不把 WebSocket 连接寿命或实时流订阅寿命误计成慢请求。</summary>
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next) {
        if (invocation.Hub is SortingRealtimeHub) return await next(invocation);
        using var activity = new Activity("SignalR." + invocation.HubMethodName).SetIdFormat(ActivityIdFormat.W3C).Start();
        using var scope = SlowQueryRequestScope.Begin(activity.TraceId.ToString());
        var started = Stopwatch.GetTimestamp();
        Exception? failure = null;
        try { return await next(invocation); }
        catch (Exception exception) { failure = exception; throw; }
        finally {
            // 只记录方法名，不采集机器密钥、图片分块或任意方法参数。
            pipeline.Collect("SIGNALR " + invocation.Hub.GetType().Name + "." + invocation.HubMethodName,
                Stopwatch.GetElapsedTime(started), exception: failure, observation: scope.Snapshot(activity.TraceId.ToString()));
        }
    }
}
