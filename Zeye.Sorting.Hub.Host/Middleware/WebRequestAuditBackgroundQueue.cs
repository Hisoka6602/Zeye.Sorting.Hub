using System.Threading.Channels;
using NLog;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计后台队列（有界，含丢弃保护）。
/// </summary>
public sealed class WebRequestAuditBackgroundQueue {
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
    /// 创建后台队列。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    public WebRequestAuditBackgroundQueue(int capacity) {
        var normalizedCapacity = Math.Max(1, capacity);
        _channel = Channel.CreateBounded<WebRequestAuditBackgroundEntry>(new BoundedChannelOptions(normalizedCapacity) {
            FullMode = BoundedChannelFullMode.DropWrite,
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
    /// 尝试入队，不阻塞请求线程。
    /// </summary>
    /// <param name="entry">队列项。</param>
    /// <returns>入队成功返回 true。</returns>
    public bool TryEnqueue(WebRequestAuditBackgroundEntry entry) {
        if (_channel.Writer.TryWrite(entry)) {
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedCount);
        NLogLogger.Warn("Web 请求审计后台队列已满，触发丢弃保护。DroppedCount={DroppedCount}, TraceId={TraceId}, CorrelationId={CorrelationId}", dropped, entry.TraceId, entry.CorrelationId);
        return false;
    }
}
