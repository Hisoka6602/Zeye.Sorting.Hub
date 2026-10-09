namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>按来源串行化接入和目录删除，避免首次注册与清理误建记录同时提交。</summary>
public static class FusionSourceGate {
    /// <summary>固定数量的进程内互斥条带，数据库唯一键和版本令牌继续保护跨进程写入。</summary>
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    /// <summary>执行同一来源的互斥操作，取消或异常时释放条带，不累积来源键对象。</summary>
    public static async Task<T> RunAsync<T>(string source, Func<Task<T>> action, CancellationToken cancellationToken) {
        var gate = Gates[(uint)StringComparer.Ordinal.GetHashCode(source) % (uint)Gates.Length];
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }
}
