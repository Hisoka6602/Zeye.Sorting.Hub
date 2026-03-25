using System;
using System.IO;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 读取时抛出异常的请求体流测试桩。
/// </summary>
public sealed class ThrowOnReadStream : Stream {
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => 20;

    /// <inheritdoc />
    public override long Position { get; set; }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) {
        throw new IOException("test-read-fault");
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) {
        Position = offset;
        return Position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }
}
