using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>计量文本大字段的延迟读取，并保留取消及异常语义。</summary>
internal sealed class SlowQueryTextReader : TextReader {
    /// <summary>保证提供器文本读取器只释放一次。</summary>
    private int _disposeStarted;
    /// <summary>提供器文本读取器。</summary>
    private readonly TextReader _inner;
    /// <summary>所属命令的计量状态。</summary>
    private readonly SlowQueryExecution _execution;
    /// <summary>关联提供器文本读取器和计量状态。</summary>
    internal SlowQueryTextReader(TextReader inner, SlowQueryExecution execution) { _inner = inner; _execution = execution; }
    /// <inheritdoc />
    public override int Peek() {
        var start = Stopwatch.GetTimestamp();
        try { var result = _inner.Peek(); _execution.ReadFinished(start); return result; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int Read() {
        var start = Stopwatch.GetTimestamp();
        try { var value = _inner.Read(); _execution.ReadFinished(start); return value; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));
    /// <inheritdoc />
    public override int Read(Span<char> buffer) {
        var start = Stopwatch.GetTimestamp();
        try { var count = _inner.Read(buffer); _execution.ReadFinished(start); return count; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override Task<int> ReadAsync(char[] buffer, int index, int count) => ReadAsync(buffer.AsMemory(index, count)).AsTask();
    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default) {
        var start = Stopwatch.GetTimestamp();
        try { var count = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); _execution.ReadFinished(start); return count; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        try {
            if (disposing && Interlocked.Exchange(ref _disposeStarted, 1) == 0) {
                var start = Stopwatch.GetTimestamp();
                try { _inner.Dispose(); _execution.ReadFinished(start); }
                catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
            }
        }
        finally { base.Dispose(disposing); }
    }
}
