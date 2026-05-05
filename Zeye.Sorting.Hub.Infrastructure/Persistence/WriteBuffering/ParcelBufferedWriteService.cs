using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// Parcel 缓冲写入服务。
/// </summary>
public sealed class ParcelBufferedWriteService : IBufferedWriteService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 有界写入通道。
    /// </summary>
    private readonly BoundedWriteChannel<BufferedParcelWriteItem> _writeChannel;

    /// <summary>
    /// 缓冲写入配置。
    /// </summary>
    private readonly BufferedWriteOptions _options;

    /// <summary>
    /// 初始化 Parcel 缓冲写入服务。
    /// </summary>
    /// <param name="writeChannel">有界写入通道。</param>
    /// <param name="options">缓冲写入配置。</param>
    public ParcelBufferedWriteService(
        BoundedWriteChannel<BufferedParcelWriteItem> writeChannel,
        IOptions<BufferedWriteOptions> options) {
        _writeChannel = writeChannel;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<BufferedWriteResult> EnqueueAsync(Parcel[] parcels, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(parcels);
        cancellationToken.ThrowIfCancellationRequested();

        if (parcels.Length == 0) {
            return Task.FromResult(new BufferedWriteResult {
                AcceptedCount = 0,
                RejectedCount = 0,
                QueueDepth = _writeChannel.Depth,
                IsBackpressureTriggered = false,
                Message = "未提供待入队包裹。"
            });
        }

        if (!_options.IsEnabled) {
            Logger.Warn("Parcel 批量缓冲写入未启用，拒绝本次请求。Count={Count}", parcels.Length);
            return Task.FromResult(new BufferedWriteResult {
                AcceptedCount = 0,
                RejectedCount = parcels.Length,
                QueueDepth = _writeChannel.Depth,
                IsBackpressureTriggered = false,
                Message = "批量缓冲写入当前未启用。"
            });
        }

        var acceptedCount = 0;
        var rejectedCount = 0;
        var isBackpressureTriggered = false;

        // 步骤 1：逐条尝试入队；当队列深度达到背压阈值时，直接拒绝剩余请求，避免继续堆积。
        foreach (var parcel in parcels) {
            if (_writeChannel.Depth >= _options.BackpressureRejectThreshold) {
                isBackpressureTriggered = true;
                rejectedCount += parcels.Length - acceptedCount - rejectedCount;
                break;
            }

            var writeItem = new BufferedParcelWriteItem(
                Parcel: parcel,
                EnqueuedAt: DateTime.Now,
                RetryCount: 0,
                LastErrorMessage: null,
                LastRetryAtLocal: null);
            if (_writeChannel.TryEnqueue(writeItem)) {
                acceptedCount++;
                continue;
            }

            rejectedCount++;
        }

        if (isBackpressureTriggered) {
            Logger.Warn(
                "Parcel 批量缓冲写入触发背压拒绝。AcceptedCount={AcceptedCount}, RejectedCount={RejectedCount}, QueueDepth={QueueDepth}, Threshold={Threshold}",
                acceptedCount,
                rejectedCount,
                _writeChannel.Depth,
                _options.BackpressureRejectThreshold);
        }

        return Task.FromResult(new BufferedWriteResult {
            AcceptedCount = acceptedCount,
            RejectedCount = rejectedCount,
            QueueDepth = _writeChannel.Depth,
            IsBackpressureTriggered = isBackpressureTriggered,
            Message = isBackpressureTriggered
                ? "队列已达到背压阈值，剩余请求已被拒绝。"
                : rejectedCount > 0
                    ? "部分请求因通道已满未能入队。"
                    : "批量请求已写入缓冲队列。"
        });
    }
}
