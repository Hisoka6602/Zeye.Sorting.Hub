using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 分表运行期巡检托管服务。
/// </summary>
public sealed class ShardingInspectionHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 分表巡检服务。
    /// </summary>
    private readonly ShardingTableInspectionService _inspectionService;

    /// <summary>
    /// 巡检配置。
    /// </summary>
    private readonly ShardingRuntimeInspectionOptions _options;

    /// <summary>
    /// 初始化分表运行期巡检托管服务。
    /// </summary>
    /// <param name="inspectionService">分表巡检服务。</param>
    /// <param name="options">巡检配置。</param>
    public ShardingInspectionHostedService(
        ShardingTableInspectionService inspectionService,
        IOptions<ShardingRuntimeInspectionOptions> options) {
        _inspectionService = inspectionService;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.IsEnabled) {
            Logger.Warn("分表运行期巡检未启用，托管服务退出。");
            return;
        }

        var interval = TimeSpan.FromMinutes(_options.InspectionIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _inspectionService.InspectAsync(stoppingToken);
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Warn("分表运行期巡检托管服务收到停止信号。");
                break;
            }
            catch (Exception ex) {
                Logger.Error(ex, "分表运行期巡检托管服务发生异常。");
                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
