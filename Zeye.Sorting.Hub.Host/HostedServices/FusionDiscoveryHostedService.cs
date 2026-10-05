using Zeye.Sorting.Hub.Application.Abstractions.Integrations;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>独立 UDP 设备发现的宿主生命周期入口。</summary>
public sealed class FusionDiscoveryHostedService(IFusionDiscoveryService discovery) : BackgroundService {
    /// <summary>启动与取消诊断日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>启用时交由发现服务监听，暂时端口冲突仅影响发现并有界重试，不退出主要业务服务。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try { await discovery.ListenAsync(stoppingToken); return; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { Logger.Debug("Fusion 发现监听已取消。"); return; }
            catch (Exception exception) when (exception is System.Net.Sockets.SocketException or IOException) {
                Logger.Error(exception, "Fusion 发现端口暂不可用，30 秒后重试；机器接收入口继续工作。");
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { Logger.Debug("Fusion 发现端口重试已取消。"); return; }
            }
            catch (Exception exception) { Logger.Error(exception, "Fusion 发现配置无效，启动失败。"); throw; }
        }
    }
}
