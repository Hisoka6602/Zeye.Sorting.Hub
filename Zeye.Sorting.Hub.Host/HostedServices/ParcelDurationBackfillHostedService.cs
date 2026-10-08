using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>启动迁移完成后自动补齐历史耗时索引，重启或跨实例竞争不会丢失进度。</summary>
public sealed class ParcelDurationBackfillHostedService(ParcelDurationBackfillService backfill, ParcelDurationCallProjectionService projection) : BackgroundService {
    /// <summary>后台补齐故障与恢复日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>一次只处理有界批次，历史补齐完成后低频检查新周期。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            var changed = 0;
            try {
                changed = await backfill.RunBatchAsync(stoppingToken);
                changed += await projection.RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { Logger.Error(exception, "历史耗时窄索引补齐失败，保留已提交游标并继续恢复。"); }
            await Task.Delay(changed > 0 ? TimeSpan.FromMilliseconds(20) : TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
