using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>关系库初始化后导入旧配置；只复制配置白名单，保留原版本和凭据保护格式。</summary>
public sealed class LegacyConfigurationMigrationHostedService(IDbContextFactory<SortingHubDbContext> factory,
    IConfigurationDocumentStore configurations) : IHostedService {
    /// <summary>在业务库初始化后导入旧版配置文档。</summary>
    public async Task StartAsync(CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        string[] keys = ["operations-policy", "rules-parcel", "rules-exception", "fusion-ingestion-directory"];
        var documents = await db.Set<ManagedDocument>().AsNoTracking().Where(x => keys.Contains(x.Key)).ToArrayAsync(cancellationToken);
        configurations.Import(documents);
        ClassificationRuleSnapshotCache.For(factory).UseConfigurationStore(configurations);
    }
    /// <summary>结束配置导入服务生命周期。</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
