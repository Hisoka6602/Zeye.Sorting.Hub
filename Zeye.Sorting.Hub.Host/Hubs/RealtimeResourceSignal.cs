using System.Collections.Concurrent;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>合并业务变更通知，限制每个连接的只读订阅，防止无限流占用资源。</summary>
public sealed class RealtimeResourceSignal {
    /// <summary>每个连接最多持有 16 个资源订阅。</summary>
    private readonly ConcurrentDictionary<string, int> _subscriptions = new(StringComparer.Ordinal);
    /// <summary>下一次成功业务写入时完成的共享唤醒任务。</summary>
    private TaskCompletionSource _changed = NewSignal();

    /// <summary>创建异步唤醒源，避免业务写线程同步运行订阅延续。</summary>
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>先捕获变更任务，再读取快照，避免漏掉写入通知。</summary>
    public Task Change => Volatile.Read(ref _changed).Task;
    /// <summary>成功写入后广播轻量通知，敏感快照仍按每个连接当前权限查询。</summary>
    public void Notify() => Interlocked.Exchange(ref _changed, NewSignal()).TrySetResult();

    /// <summary>申请订阅槽位，连接数和每个连接的流数量均受限。</summary>
    public bool TrySubscribe(string connectionId) {
        if (!_subscriptions.ContainsKey(connectionId) && _subscriptions.Count >= 96) return false;
        while (true) {
            var count = _subscriptions.GetOrAdd(connectionId, 0);
            if (count >= 16) return false;
            if (_subscriptions.TryUpdate(connectionId, count + 1, count)) return true;
        }
    }

    /// <summary>释放流槽位，连接断开不留下注册记录。</summary>
    public void Unsubscribe(string connectionId) {
        while (_subscriptions.TryGetValue(connectionId, out var count)) {
            if (count <= 1) { if (_subscriptions.TryRemove(new KeyValuePair<string, int>(connectionId, count))) return; }
            else if (_subscriptions.TryUpdate(connectionId, count - 1, count)) return;
        }
    }
}
