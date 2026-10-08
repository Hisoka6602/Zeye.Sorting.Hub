using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>外部 LiteDB 修改通过 .NET reload token 通知配置订阅者。</summary>
public sealed class ConfigurationReloadHostedService(RuntimeConfigurationProvider source, IConfiguration configuration) : BackgroundService {
    /// <summary>周期检查配置变更并发出热更新通知。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var seconds = Math.Clamp(configuration.GetValue("ConfigurationStorage:ReloadIntervalSeconds", 1), 1, 60);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) source.TryReload(); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
