using System.Diagnostics.Metrics;

namespace Zeye.Sorting.Hub.SharedKernel.Diagnostics;

/// <summary>
/// 分拣中心高频链路的低开销运行时指标。
/// </summary>
public static class SortingHubPerformanceMetrics {
    /// <summary>
    /// 供 OpenTelemetry、dotnet-counters 和其他监听器订阅的仪表名称。
    /// </summary>
    public const string MeterName = "Zeye.Sorting.Hub.Performance";

    /// <summary>
    /// 性能指标仪表。
    /// </summary>
    private static readonly Meter PerformanceMeter = new(MeterName, "1.0.0");
    /// <summary>
    /// 审计队列成功入队计数器。
    /// </summary>
    private static readonly Counter<long> AuditEnqueuedCounter = PerformanceMeter.CreateCounter<long>("sorting.audit.enqueued");
    /// <summary>
    /// 审计队列丢弃计数器。
    /// </summary>
    private static readonly Counter<long> AuditDroppedCounter = PerformanceMeter.CreateCounter<long>("sorting.audit.dropped");
    /// <summary>
    /// 审计队列深度变化计数器。
    /// </summary>
    private static readonly UpDownCounter<long> AuditQueueDepthCounter = PerformanceMeter.CreateUpDownCounter<long>("sorting.audit.queue.depth");
    /// <summary>
    /// 批量写入成功入队计数器。
    /// </summary>
    private static readonly Counter<long> BufferedWriteEnqueuedCounter = PerformanceMeter.CreateCounter<long>("sorting.buffered_write.enqueued");
    /// <summary>
    /// 批量写入丢弃计数器。
    /// </summary>
    private static readonly Counter<long> BufferedWriteDroppedCounter = PerformanceMeter.CreateCounter<long>("sorting.buffered_write.dropped");
    /// <summary>
    /// 批量写入队列深度变化计数器。
    /// </summary>
    private static readonly UpDownCounter<long> BufferedWriteQueueDepthCounter = PerformanceMeter.CreateUpDownCounter<long>("sorting.buffered_write.queue.depth");
    /// <summary>
    /// 慢查询样本采集计数器。
    /// </summary>
    private static readonly Counter<long> SlowQueryCollectedCounter = PerformanceMeter.CreateCounter<long>("sorting.slow_query.collected");
    /// <summary>
    /// 慢查询样本丢弃计数器。
    /// </summary>
    private static readonly Counter<long> SlowQueryDroppedCounter = PerformanceMeter.CreateCounter<long>("sorting.slow_query.dropped");
    /// <summary>
    /// Outbox 处理计数器。
    /// </summary>
    private static readonly Counter<long> OutboxProcessedCounter = PerformanceMeter.CreateCounter<long>("sorting.outbox.processed");
    /// <summary>
    /// Outbox 成功计数器。
    /// </summary>
    private static readonly Counter<long> OutboxSucceededCounter = PerformanceMeter.CreateCounter<long>("sorting.outbox.succeeded");
    /// <summary>
    /// Outbox 失败计数器。
    /// </summary>
    private static readonly Counter<long> OutboxFailedCounter = PerformanceMeter.CreateCounter<long>("sorting.outbox.failed");
    /// <summary>
    /// Outbox 批次耗时直方图。
    /// </summary>
    private static readonly Histogram<decimal> OutboxBatchDurationHistogram = PerformanceMeter.CreateHistogram<decimal>("sorting.outbox.batch.duration", "ms");

    /// <summary>
    /// 记录一条审计项成功入队。
    /// </summary>
    public static void RecordAuditEnqueued() {
        AuditEnqueuedCounter.Add(1L);
        AuditQueueDepthCounter.Add(1L);
    }

    /// <summary>
    /// 记录一条审计项被消费。
    /// </summary>
    public static void RecordAuditDequeued() {
        AuditQueueDepthCounter.Add(-1L);
    }

    /// <summary>
    /// 记录一条审计项因背压被丢弃。
    /// </summary>
    public static void RecordAuditDropped() {
        AuditDroppedCounter.Add(1L);
    }

    /// <summary>
    /// 记录一条缓冲写入项成功入队。
    /// </summary>
    public static void RecordBufferedWriteEnqueued() {
        BufferedWriteEnqueuedCounter.Add(1L);
        BufferedWriteQueueDepthCounter.Add(1L);
    }

    /// <summary>
    /// 记录一条缓冲写入项被消费。
    /// </summary>
    public static void RecordBufferedWriteDequeued() {
        BufferedWriteQueueDepthCounter.Add(-1L);
    }

    /// <summary>
    /// 记录一条缓冲写入项因背压被丢弃。
    /// </summary>
    public static void RecordBufferedWriteDropped() {
        BufferedWriteDroppedCounter.Add(1L);
    }

    /// <summary>
    /// 记录慢查询样本采集及本轮被淘汰的样本数。
    /// </summary>
    /// <param name="droppedCount">本轮被淘汰的样本数。</param>
    public static void RecordSlowQueryCollected(int droppedCount) {
        SlowQueryCollectedCounter.Add(1L);
        if (droppedCount > 0) {
            SlowQueryDroppedCounter.Add(droppedCount);
        }
    }

    /// <summary>
    /// 记录一个 Outbox 批次的处理结果和耗时。
    /// </summary>
    /// <param name="processedCount">处理数量。</param>
    /// <param name="succeededCount">成功数量。</param>
    /// <param name="failedCount">失败数量。</param>
    /// <param name="elapsedMilliseconds">批次耗时毫秒数。</param>
    public static void RecordOutboxBatch(int processedCount, int succeededCount, int failedCount, decimal elapsedMilliseconds) {
        OutboxProcessedCounter.Add(processedCount);
        OutboxSucceededCounter.Add(succeededCount);
        OutboxFailedCounter.Add(failedCount);
        OutboxBatchDurationHistogram.Record(elapsedMilliseconds);
    }
}
