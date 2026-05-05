using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 批量缓冲写入队列健康检查。
/// </summary>
public sealed class BufferedWriteQueueHealthCheck : IHealthCheck {
    /// <summary>
    /// 刷新服务。
    /// </summary>
    private readonly ParcelBatchWriteFlushService _flushService;

    /// <summary>
    /// 缓冲写入配置。
    /// </summary>
    private readonly BufferedWriteOptions _options;

    /// <summary>
    /// 初始化批量缓冲写入队列健康检查。
    /// </summary>
    /// <param name="flushService">刷新服务。</param>
    /// <param name="options">缓冲写入配置。</param>
    public BufferedWriteQueueHealthCheck(
        ParcelBatchWriteFlushService flushService,
        IOptions<BufferedWriteOptions> options) {
        _flushService = flushService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var snapshot = _flushService.GetMetricsSnapshot();
        var data = BuildHealthData(snapshot);
        if (!snapshot.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("批量缓冲写入未启用。", data: data));
        }

        if (snapshot.QueueDepth >= _options.ChannelCapacity || snapshot.DeadLetterCount >= _options.DeadLetterCapacity) {
            return Task.FromResult(HealthCheckResult.Unhealthy("批量缓冲写入队列已触达容量上限。", data: data));
        }

        if (snapshot.DeadLetterCount > 0 || snapshot.IsBackpressureTriggered || snapshot.DroppedCount > 0 || snapshot.LastFailedFlushAtLocal.HasValue) {
            return Task.FromResult(HealthCheckResult.Degraded("批量缓冲写入队列存在背压、丢弃或死信。", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("批量缓冲写入队列正常。", data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="snapshot">指标快照。</param>
    /// <returns>附加数据字典。</returns>
    private IReadOnlyDictionary<string, object> BuildHealthData(BatchWriteMetricsSnapshot snapshot) {
        var data = new Dictionary<string, object> {
            ["isEnabled"] = snapshot.IsEnabled,
            ["queueDepth"] = snapshot.QueueDepth,
            ["deadLetterCount"] = snapshot.DeadLetterCount,
            ["droppedCount"] = snapshot.DroppedCount,
            ["successfulFlushCount"] = snapshot.SuccessfulFlushCount,
            ["failedFlushCount"] = snapshot.FailedFlushCount,
            ["totalFlushedCount"] = snapshot.TotalFlushedCount,
            ["isBackpressureTriggered"] = snapshot.IsBackpressureTriggered,
            ["channelCapacity"] = _options.ChannelCapacity,
            ["backpressureRejectThreshold"] = _options.BackpressureRejectThreshold,
            ["deadLetterCapacity"] = _options.DeadLetterCapacity
        };
        if (snapshot.LastSuccessfulFlushAtLocal.HasValue) {
            data["lastSuccessfulFlushAtLocal"] = snapshot.LastSuccessfulFlushAtLocal.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (snapshot.LastFailedFlushAtLocal.HasValue) {
            data["lastFailedFlushAtLocal"] = snapshot.LastFailedFlushAtLocal.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastFailureMessage)) {
            data["lastFailureMessage"] = snapshot.LastFailureMessage;
        }

        return data;
    }
}
