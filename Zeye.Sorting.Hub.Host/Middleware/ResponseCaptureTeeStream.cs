using System.Text;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// 响应写入与采集双写流，主流实时写出，采集流按上限截断。
/// </summary>
public sealed class ResponseCaptureTeeStream : Stream {
    /// <summary>
    /// 原始响应流。
    /// </summary>
    private readonly Stream _inner;

    /// <summary>
    /// 最大采集字节长度。
    /// </summary>
    private readonly int _captureMaxBytes;

    /// <summary>
    /// 最大采集字符长度。
    /// </summary>
    private readonly int _captureMaxChars;

    /// <summary>
    /// 已采集字节缓冲。
    /// </summary>
    private readonly MemoryStream _capturedBytes = new();

    /// <summary>
    /// 写出的响应总字节数。
    /// </summary>
    private long _totalWrittenBytes;

    /// <summary>
    /// 是否已发生截断。
    /// </summary>
    private bool _isTruncated;

    /// <summary>
    /// 创建双写流实例。
    /// </summary>
    /// <param name="inner">原始响应流。</param>
    /// <param name="captureMaxLength">最大采集字符长度。</param>
    public ResponseCaptureTeeStream(Stream inner, int captureMaxLength) {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _captureMaxChars = Math.Max(0, captureMaxLength);
        _captureMaxBytes = _captureMaxChars == 0 ? 0 : Math.Max(1, Encoding.UTF8.GetMaxByteCount(_captureMaxChars));
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
    public override long Position {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) {
        CaptureBytes(buffer.AsSpan(offset, count));
        _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        CaptureBytes(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        CaptureBytes(buffer.AsSpan(offset, count));
        return _inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <summary>
    /// 构建响应采集结果。
    /// </summary>
    /// <returns>响应采集结果。</returns>
    public ResponseCaptureResult BuildCaptureResult() {
        var hasBody = _totalWrittenBytes > 0;
        if (_captureMaxBytes == 0) {
            return new ResponseCaptureResult(string.Empty, hasBody, hasBody, _totalWrittenBytes);
        }

        _capturedBytes.Position = 0;
        using var reader = new StreamReader(_capturedBytes, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var decoded = reader.ReadToEnd();
        if (decoded.Length > _captureMaxChars) {
            return new ResponseCaptureResult(decoded[.._captureMaxChars], hasBody, true, _totalWrittenBytes);
        }

        return new ResponseCaptureResult(decoded, hasBody, _isTruncated, _totalWrittenBytes);
    }

    /// <summary>
    /// 采集写入字节，超上限后仅标记截断。
    /// </summary>
    /// <param name="buffer">写入缓冲。</param>
    private void CaptureBytes(ReadOnlySpan<byte> buffer) {
        _totalWrittenBytes += buffer.Length;
        if (_captureMaxBytes <= 0 || buffer.IsEmpty) {
            if (_captureMaxBytes <= 0 && !_isTruncated && _totalWrittenBytes > 0) {
                _isTruncated = true;
            }

            return;
        }

        var remain = _captureMaxBytes - (int)_capturedBytes.Length;
        if (remain <= 0) {
            _isTruncated = true;
            return;
        }

        var writeCount = Math.Min(remain, buffer.Length);
        _capturedBytes.Write(buffer[..writeCount]);
        if (writeCount < buffer.Length) {
            _isTruncated = true;
        }
    }
}
