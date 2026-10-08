using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>计量顺序读取大字段的实际流调用，不将消费方等待计入提供器读取。</summary>
internal sealed class SlowQueryReaderStream : Stream {
    /// <summary>提供器返回的原始流。</summary>
    private readonly Stream _inner;
    /// <summary>所属命令的计量状态。</summary>
    private readonly SlowQueryExecution _execution;
    /// <summary>关联原始流与所属命令。</summary>
    internal SlowQueryReaderStream(Stream inner, SlowQueryExecution execution) { _inner = inner; _execution = execution; }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
    /// <inheritdoc />
    public override int Read(Span<byte> buffer) {
        var start = Stopwatch.GetTimestamp();
        try { var count = _inner.Read(buffer); _execution.ReadFinished(start); return count; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        var start = Stopwatch.GetTimestamp();
        try { var count = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false); _execution.ReadFinished(start); return count; }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;
    /// <inheritdoc />
    public override bool CanSeek => _inner.CanSeek;
    /// <inheritdoc />
    public override bool CanWrite => _inner.CanWrite;
    /// <inheritdoc />
    public override long Length => _inner.Length;
    /// <inheritdoc />
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    /// <inheritdoc />
    public override void Flush() => _inner.Flush();
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    /// <inheritdoc />
    protected override void Dispose(bool disposing) {
        if (!disposing) return;
        var start = Stopwatch.GetTimestamp();
        try { _inner.Dispose(); _execution.ReadFinished(start); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
    /// <inheritdoc />
    public override async ValueTask DisposeAsync() {
        var start = Stopwatch.GetTimestamp();
        try { await _inner.DisposeAsync().ConfigureAwait(false); _execution.ReadFinished(start); }
        catch (Exception exception) { _execution.ReadFinished(start); _execution.Complete(exception, true); throw; }
    }
}
