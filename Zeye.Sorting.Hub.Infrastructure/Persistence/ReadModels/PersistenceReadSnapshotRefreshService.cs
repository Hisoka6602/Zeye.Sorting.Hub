using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>在接收业务之前预热查询元数据，后台同步多实例配置，不在事实热路径读取配置表。</summary>
public sealed class PersistenceReadSnapshotRefreshService(IDbContextFactory<SortingHubDbContext> factory,
    ParcelPartitionStore partitions) : BackgroundService {
    /// <summary>启动、后台同步及取消诊断日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>启动先完成两份快照加载，数据库不可用时不得使用伪造的空规则进入接收状态。</summary>
    public override async Task StartAsync(CancellationToken cancellationToken) {
        await RefreshAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }
    /// <summary>跨 Hub 的分表及规则变更最多在一个刷新周期内同步，本实例保存即时发布。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try {
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                try { await RefreshAsync(stoppingToken); }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    Logger.Error(exception, "后台持久化读快照同步失败，将继续刷新。");
                }
            }
        }
        catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested) {
            Logger.Debug(exception, "持久化读快照同步已取消。");
        }
    }
    /// <summary>低频读取 EF Core 配置实体，元数据与业务查询结果不共用缓存。</summary>
    private async Task RefreshAsync(CancellationToken cancellationToken) {
        await partitions.RefreshReadCatalogAsync(cancellationToken);
        await ClassificationRuleSnapshotCache.For(factory).RefreshAsync(cancellationToken);
    }
}
