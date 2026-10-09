using System.Runtime.CompilerServices;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>隔离的真实 SignalR 异常入口，用于验证调用与流迭代阶段均能落盘。</summary>
public sealed class ExceptionLoggingTestHub : Microsoft.AspNetCore.SignalR.Hub {
    /// <summary>制造普通调用失败，原始原因只进入服务端异常日志。</summary>
    public Task Fail() {
        Context.ConnectionAborted.ThrowIfCancellationRequested();
        throw new InvalidOperationException("hub-original-failure", new IOException("hub-inner-failure"));
    }

    /// <summary>在已经返回首个流元素后失败，覆盖 Hub 调用过滤器之外的异常路径。</summary>
    public async IAsyncEnumerable<int> StreamFailure([EnumeratorCancellation] CancellationToken token) {
        yield return 1;
        await Task.Yield();
        token.ThrowIfCancellationRequested();
        Context.ConnectionAborted.ThrowIfCancellationRequested();
        throw new InvalidOperationException("stream-original-failure");
    }
}
