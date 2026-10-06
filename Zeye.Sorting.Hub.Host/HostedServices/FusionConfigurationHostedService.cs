using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Hubs;
namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>启动在数据库初始化之后完成导入；共享数据库变更也会跨实例加载。</summary>
public sealed class FusionConfigurationHostedService(FusionConfigurationService configuration, RealtimeResourceSignal changes) : BackgroundService {
    /// <summary>记录刷新异常及堆栈，不输出配置正文或凭据。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>在数据库初始化后加载接入目录。</summary>
    public override async Task StartAsync(CancellationToken ct) { await configuration.InitializeAsync(ct); await base.StartAsync(ct); }
    /// <summary>刷新其他实例修改的配置并通知实时订阅。</summary>
    protected override async Task ExecuteAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                var revision = configuration.Snapshot.Revision;
                await configuration.RefreshAsync(ct);
                if (configuration.Snapshot.Revision != revision) changes.Notify();
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception exception) { Logger.Warn(exception, "Fusion 登记目录刷新失败，保留最后有效配置。"); }
        }
    }
}
