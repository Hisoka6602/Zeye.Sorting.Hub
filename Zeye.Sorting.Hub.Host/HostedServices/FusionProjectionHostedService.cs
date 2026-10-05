using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Application.Services.Fusion;
using Zeye.Sorting.Hub.Host.Hubs;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>承载耐久事实投影生命周期，具体包裹处理仍由应用用例负责。</summary>
public sealed class FusionProjectionHostedService(IServiceScopeFactory scopes, IFusionIngestionGateway ingress, RealtimeResourceSignal changes) : BackgroundService {
    /// <summary>后台任务日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>有界投影循环和低频上传维护；单次故障不会退出恢复服务。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var maintenance = DateTime.Now;
        while (!stoppingToken.IsCancellationRequested) {
            var changed = 0;
            try {
                await using var scope = scopes.CreateAsyncScope();
                changed = await scope.ServiceProvider.GetRequiredService<FusionProjectionService>().ProjectAsync(stoppingToken);
                if (changed > 0) changes.Notify();
                if (DateTime.Now >= maintenance) { await ingress.MaintainUploadsAsync(stoppingToken); maintenance = DateTime.Now.AddHours(1); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { Logger.Debug("Fusion 事实投影已取消。"); break; }
            catch (Exception exception) { Logger.Error(exception, "Fusion 耐久事实投影或图片维护失败，将继续恢复。"); }
            await Task.Delay(TimeSpan.FromMilliseconds(changed > 0 ? 250 : 2000), stoppingToken);
        }
    }
}
