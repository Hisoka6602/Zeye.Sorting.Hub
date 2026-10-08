namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在读取大字段阶段保留指定异常实例，验证计量不能改变异常语义。</summary>
internal sealed class SlowQueryFailingStream(Exception failure) : MemoryStream {
    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => throw failure;
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
}
