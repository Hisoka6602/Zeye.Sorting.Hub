using System.Threading.Channels;
using NLog;
using Zeye.Sorting.Hub.SharedKernel.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计后台队列（有界，含丢弃保护）。
/// </summary>
public sealed class WebRequestAuditBuffer {
    /// <summary>
    /// NLog 记录器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();
    /// <summary>
    /// 有界通道实例。
    /// </summary>
    private readonly Channel<WebRequestAuditBackgroundEntry> _channel;
    /// <summary>
    /// 丢弃计数。
    /// </summary>
    private long _droppedCount;
    /// <summary>
    /// 当前队列深度。
    /// </summary>
    private long _depth;
    /// <summary>
    /// 上次输出丢弃告警的本地时间刻度。
    /// </summary>
    private long _lastDropLogTicks;
    /// <summary>
    /// 丢弃告警最小输出间隔。
    /// </summary>
    private readonly TimeSpan _dropLogInterval;

    /// <summary>
    /// 创建后台队列。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    public WebRequestAuditBuffer(int capacity)
        : this(capacity, TimeSpan.FromSeconds(30)) {
    }

    /// <summary>
    /// 创建支持丢弃日志聚合的后台队列。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    /// <param name="dropLogInterval">丢弃日志聚合间隔。</param>
    public WebRequestAuditBuffer(int capacity, TimeSpan dropLogInterval) {
        var normalizedCapacity = Math.Max(1, capacity);
        _dropLogInterval = dropLogInterval <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : dropLogInterval;
        _channel = Channel.CreateBounded<WebRequestAuditBackgroundEntry>(new BoundedChannelOptions(normalizedCapacity) {
            // 使用 Wait 模式配合非阻塞 TryWrite：满队列时明确返回 false，便于准确统计丢弃。
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// 后台消费读取器。
    /// </summary>
    public ChannelReader<WebRequestAuditBackgroundEntry> Reader => _channel.Reader;

    /// <summary>
    /// 当前累计丢弃数量。
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>
    /// 当前排队等待写入的审计日志数量。
    /// </summary>
    public long Depth => Interlocked.Read(ref _depth);

    /// <summary>
    /// 尝试入队，不阻塞请求线程。
    /// </summary>
    /// <param name="entry">队列项。</param>
    /// <returns>入队成功返回 true。</returns>
    public bool TryEnqueue(WebRequestAuditBackgroundEntry entry) {
        if (_channel.Writer.TryWrite(entry)) {
            Interlocked.Increment(ref _depth);
            SortingHubPerformanceMetrics.RecordAuditEnqueued();
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedCount);
        SortingHubPerformanceMetrics.RecordAuditDropped();
        TryLogAggregatedDrop(dropped, entry);
        return false;
    }

    /// <summary>
    /// 标记一个队列项已被消费者取走。
    /// </summary>
    public void MarkDequeued() {
        Interlocked.Decrement(ref _depth);
        SortingHubPerformanceMetrics.RecordAuditDequeued();
    }

    /// <summary>
    /// 按固定时间窗口聚合输出队列丢弃告警。
    /// </summary>
    /// <param name="droppedCount">累计丢弃数量。</param>
    /// <param name="entry">最近被丢弃的队列项。</param>
    private void TryLogAggregatedDrop(long droppedCount, WebRequestAuditBackgroundEntry entry) {
        var nowTicks = DateTime.Now.Ticks;
        var previousTicks = Interlocked.Read(ref _lastDropLogTicks);
        if (previousTicks != 0L && nowTicks - previousTicks < _dropLogInterval.Ticks) {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastDropLogTicks, nowTicks, previousTicks) == previousTicks) {
            NLogLogger.Warn(
                "Web 请求审计后台队列已满，丢弃告警已聚合。DroppedCount={DroppedCount}, QueueDepth={QueueDepth}, TraceId={TraceId}, CorrelationId={CorrelationId}",
                droppedCount,
                Depth,
                entry.TraceId,
                entry.CorrelationId);
        }
    }
}
