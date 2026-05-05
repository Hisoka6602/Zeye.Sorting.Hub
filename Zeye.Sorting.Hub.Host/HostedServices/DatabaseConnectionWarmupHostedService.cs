using Microsoft.Extensions.Hosting;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 数据库连接预热托管服务。
/// </summary>
public sealed class DatabaseConnectionWarmupHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据库连接预热服务。
    /// </summary>
    private readonly DatabaseConnectionWarmupService _databaseConnectionWarmupService;

    /// <summary>
    /// 初始化 <see cref="DatabaseConnectionWarmupHostedService"/>。
    /// </summary>
    /// <param name="databaseConnectionWarmupService">数据库连接预热服务。</param>
    public DatabaseConnectionWarmupHostedService(DatabaseConnectionWarmupService databaseConnectionWarmupService) {
        _databaseConnectionWarmupService = databaseConnectionWarmupService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await _databaseConnectionWarmupService.WarmupAsync(stoppingToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "数据库连接预热托管服务执行失败");
        }
    }
}
