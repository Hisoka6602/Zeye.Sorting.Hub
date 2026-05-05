using NLog;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 数据归档后台托管服务。
/// </summary>
public sealed class DataArchiveHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 初始化数据归档后台托管服务。
    /// </summary>
    /// <param name="serviceScopeFactory">作用域工厂。</param>
    public DataArchiveHostedService(IServiceScopeFactory serviceScopeFactory) {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    /// <summary>
    /// 执行后台轮询循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("数据归档后台托管服务已启动。");
        while (!stoppingToken.IsCancellationRequested) {
            var pollDelay = TimeSpan.FromSeconds(30);
            try {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<DataArchiveHostedWorker>();
                pollDelay = worker.GetPollDelay();
                await worker.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
            catch (Exception ex) {
                Logger.Error(ex, "数据归档后台托管服务执行轮询失败。");
            }

            try {
                await Task.Delay(pollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            }
        }

        Logger.Info("数据归档后台托管服务已停止。");
    }
}
