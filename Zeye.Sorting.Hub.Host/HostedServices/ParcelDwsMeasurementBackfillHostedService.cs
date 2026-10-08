using Zeye.Sorting.Hub.Application.Abstractions.Persistence;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>初始化完成后低负载补齐DWS查询窄表，不阻塞业务接收或在页面请求中写库。</summary>
public sealed class ParcelDwsMeasurementBackfillHostedService(IParcelDwsMeasurementBackfillService backfill) : BackgroundService {
    /// <summary>补齐异常和恢复日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>每次只执行一批，失败保留游标并在下一周期恢复。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            var changed = 0;
            try { changed = await backfill.RunBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { Logger.Error(exception, "DWS窄量测索引补齐失败，保留已提交游标并继续恢复。"); }
            await Task.Delay(changed > 0 ? TimeSpan.FromMilliseconds(20) : TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
