namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>记录底层流被释放的次数，并在释放后注入原始异常以验证装饰器语义。</summary>
internal sealed class DisposeTrackingStream(Exception? failure) : MemoryStream {
    /// <summary>实际同步资源释放次数，异步基类同样调用此释放路径。</summary>
    internal int DisposeCount { get; private set; }
    /// <summary>装饰器转发到底层的异步释放次数。</summary>
    internal int AsyncDisposeCount { get; private set; }
    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (!disposing) return;
        DisposeCount++;
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    /// <inheritdoc />
    public override ValueTask DisposeAsync() {
        AsyncDisposeCount++;
        return base.DisposeAsync();
    }
}
