using Microsoft.AspNetCore.SignalR;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>独立诊断测试 Hub，不连接本地真实站点或机器工作台。</summary>
public sealed class SlowQueryTestHub : Microsoft.AspNetCore.SignalR.Hub {
    /// <summary>允许检查过滤器是否误把方法参数写入慢请求样本。</summary>
    public Task<string> Read(string value) {
        Context.ConnectionAborted.ThrowIfCancellationRequested();
        return Task.FromResult(value);
    }
}
