namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证文本读取器释放只转发一次，释放失败也不能在重入时改变原始异常。</summary>
internal sealed class DisposeTrackingTextReader(Exception? failure) : StringReader("payload") {
    /// <summary>底层文本资源的释放次数。</summary>
    internal int DisposeCount { get; private set; }
    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (!disposing) return;
        DisposeCount++;
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
