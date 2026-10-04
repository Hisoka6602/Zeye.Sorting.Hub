using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Zeye.Sorting.Hub.Contracts.Models.Realtime;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>提供认证的高频查询、持续快照和两个命名包裹用例，危险管理操作不进入实时通道。</summary>
[Authorize(AuthenticationSchemes = "SortingCookie")]
public sealed class SortingRealtimeHub(RealtimeEndpointDispatcher dispatcher, RealtimeResourceSignal changes) : Microsoft.AspNetCore.SignalR.Hub {
    /// <summary>预期流取消与连接异常日志器。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>读取白名单资源；状态码和 JSON 原文与原接口一致。</summary>
    public Task<RealtimeResponse> Read(string path) => dispatcher.ReadAsync(path, Context.GetHttpContext()!, Context.ConnectionAborted);

    /// <summary>追加处理事实，原接口验证当前写权限、输入与幂等标识，不自动重试提交。</summary>
    public Task<RealtimeResponse> AppendProcessingRecord(string json) => dispatcher.AppendProcessingRecordAsync(json, Context.GetHttpContext()!, Context.ConnectionAborted);

    /// <summary>更新指定包裹状态，复用正式状态入口的权限和业务校验。</summary>
    public Task<RealtimeResponse> UpdateParcelStatus(string id, string json) => dispatcher.UpdateParcelStatusAsync(id, json, Context.GetHttpContext()!, Context.ConnectionAborted);

    /// <summary>推送初始快照、写入后的变更及低频状态校验，取消订阅立即停止查询。</summary>
    public async IAsyncEnumerable<RealtimeResponse> Watch(string path, [EnumeratorCancellation] CancellationToken cancellationToken) {
        if (!RealtimeReadPolicy.IsAllowed(path)) throw new HubException("此资源不能实时订阅。");
        if (!changes.TrySubscribe(Context.ConnectionId)) throw new HubException("实时订阅已达上限，请关闭多余页面。");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
        RealtimeResponse? previous = null;
        try {
            while (!lifetime.IsCancellationRequested) {
                var changed = changes.Change;
                var response = await dispatcher.ReadAsync(path, Context.GetHttpContext()!, lifetime.Token);
                if (response != previous) { previous = response; yield return response; }
                if (response.StatusCode is 401 or 403) yield break;
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                await Task.WhenAny(changed, Task.Delay(TimeSpan.FromSeconds(15), delay.Token));
                await delay.CancelAsync();
                lifetime.Token.ThrowIfCancellationRequested();
                // 合并密集写入，每个资源每秒最多重新构建一次快照。
                await Task.Delay(TimeSpan.FromMilliseconds(1000), lifetime.Token);
            }
        }
        finally { changes.Unsubscribe(Context.ConnectionId); }
    }

    /// <summary>记录断线原因，流取消负责释放订阅槽位。</summary>
    public override Task OnDisconnectedAsync(Exception? exception) {
        if (exception is not null) Logger.Warn(exception, "实时连接断开。");
        return base.OnDisconnectedAsync(exception);
    }
}
