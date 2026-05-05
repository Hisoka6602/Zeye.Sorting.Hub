using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 分表预建计划托管服务。
/// </summary>
public sealed class ShardingPrebuildHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 分表预建计划服务。
    /// </summary>
    private readonly ShardingTablePrebuildService _prebuildService;

    /// <summary>
    /// 分表预建配置。
    /// </summary>
    private readonly ShardingPrebuildOptions _options;

    /// <summary>
    /// 初始化分表预建计划托管服务。
    /// </summary>
    /// <param name="prebuildService">分表预建计划服务。</param>
    /// <param name="options">分表预建配置。</param>
    public ShardingPrebuildHostedService(
        ShardingTablePrebuildService prebuildService,
        IOptions<ShardingPrebuildOptions> options) {
        _prebuildService = prebuildService;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.IsEnabled) {
            Logger.Warn("分表预建计划未启用，托管服务退出。");
            return;
        }

        try {
            var plan = await _prebuildService.BuildPlanAsync(stoppingToken);
            Logger.Info(
                "分表预建计划托管服务完成启动期计划生成：DryRun={DryRun}, PlannedCount={PlannedCount}, MissingCount={MissingCount}",
                plan.IsDryRun,
                plan.PlannedPhysicalTables.Count,
                plan.MissingPhysicalTables.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            Logger.Warn("分表预建计划托管服务收到停止信号。");
        }
        catch (Exception ex) {
            Logger.Error(ex, "分表预建计划托管服务发生异常。");
        }
    }
}
