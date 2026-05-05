using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// Parcel 批量缓冲写入后台刷新托管服务。
/// </summary>
public sealed class ParcelBatchWriteFlushHostedService : BackgroundService {
    /// <summary>
    /// 刷新服务。
    /// </summary>
    private readonly ParcelBatchWriteFlushService _flushService;

    /// <summary>
    /// 初始化 Parcel 批量缓冲写入后台刷新托管服务。
    /// </summary>
    /// <param name="flushService">刷新服务。</param>
    public ParcelBatchWriteFlushHostedService(ParcelBatchWriteFlushService flushService) {
        _flushService = flushService;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) {
        return _flushService.ProcessAsync(stoppingToken);
    }
}
