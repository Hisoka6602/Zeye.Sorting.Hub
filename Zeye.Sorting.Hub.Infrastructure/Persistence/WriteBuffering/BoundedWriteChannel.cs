using System.Threading.Channels;
using Zeye.Sorting.Hub.SharedKernel.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// 有界写入通道封装。
/// </summary>
/// <typeparam name="TItem">通道项类型。</typeparam>
public sealed class BoundedWriteChannel<TItem> {
    /// <summary>
    /// 通道实例。
    /// </summary>
    private readonly Channel<TItem> _channel;

    /// <summary>
    /// 当前深度计数。
    /// </summary>
    private int _depth;

    /// <summary>
    /// 累计丢弃计数。
    /// </summary>
    private long _droppedCount;

    /// <summary>是否仍接受生产者写入，停止后只允许消费者排空。</summary>
    private int _isAccepting = 1;

    /// <summary>
    /// 初始化有界写入通道。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    public BoundedWriteChannel(int capacity) {
        Capacity = Math.Max(1, capacity);
        // FullMode.Wait 仅配合 TryEnqueue 的 CAS 预占深度逻辑，实际容量边界由 _depth 控制。
        _channel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(Capacity) {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// 通道容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前深度。
    /// </summary>
    public int Depth => Volatile.Read(ref _depth);

    /// <summary>
    /// 累计丢弃数量。
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>是否仍接受新的缓冲记录。</summary>
    public bool IsAccepting => Volatile.Read(ref _isAccepting) != 0;

    /// <summary>关闭写入端并唤醒空队列消费者，已有记录仍可读取。</summary>
    public void Complete() {
        Interlocked.Exchange(ref _isAccepting, 0);
        _channel.Writer.TryComplete();
    }

    /// <summary>
    /// 尝试写入通道。
    /// </summary>
    /// <param name="item">通道项。</param>
    /// <returns>写入成功返回 true。</returns>
    public bool TryEnqueue(TItem item) {
        if (!IsAccepting) {
            return false;
        }

        var reservedDepth = Interlocked.Increment(ref _depth);
        if (reservedDepth > Capacity) {
            Interlocked.Decrement(ref _depth);
            Interlocked.Increment(ref _droppedCount);
            SortingHubPerformanceMetrics.RecordBufferedWriteDropped();
            return false;
        }

        if (_channel.Writer.TryWrite(item)) {
            SortingHubPerformanceMetrics.RecordBufferedWriteEnqueued();
            return true;
        }

        Interlocked.Decrement(ref _depth);
        Interlocked.Increment(ref _droppedCount);
        SortingHubPerformanceMetrics.RecordBufferedWriteDropped();
        return false;
    }

    /// <summary>
    /// 等待通道可读。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可读时返回 true。</returns>
    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    /// <summary>
    /// 尝试从通道读取一项。
    /// </summary>
    /// <param name="item">读取到的通道项。</param>
    /// <returns>读取成功返回 true。</returns>
    public bool TryDequeue(out TItem item) {
        if (_channel.Reader.TryRead(out var readItem)) {
            item = readItem;
            Interlocked.Decrement(ref _depth);
            SortingHubPerformanceMetrics.RecordBufferedWriteDequeued();
            return true;
        }

        item = default!;
        return false;
    }
}
