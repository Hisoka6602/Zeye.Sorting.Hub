using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Host.Queries;
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
    /// <summary>真实包裹物理分表执行器。</summary>
    private readonly PartitionMaintenanceService _maintenance;
    /// <summary>成对预建审计日表的执行器。</summary>
    private readonly AuditPartitionMaintenanceService _auditMaintenance;
    /// <summary>预建后即时复核实际物理表和索引。</summary>
    private readonly ShardingTableInspectionService _inspection;
    /// <summary>读取页面保存的有界预建窗口。</summary>
    private readonly OperationalPolicyService _policy;

    /// <summary>
    /// 初始化分表预建计划托管服务。
    /// </summary>
    /// <param name="prebuildService">分表预建计划服务。</param>
    /// <param name="options">分表预建配置。</param>
    /// <param name="maintenance">真实分表执行器。</param>
    /// <param name="auditMaintenance">审计热表与详情日表执行器。</param>
    /// <param name="inspection">真实分表和索引复核。</param>
    /// <param name="policy">页面保存的预建策略。</param>
    public ShardingPrebuildHostedService(
        ShardingTablePrebuildService prebuildService,
        IOptions<ShardingPrebuildOptions> options,
        PartitionMaintenanceService maintenance,
        AuditPartitionMaintenanceService auditMaintenance,
        ShardingTableInspectionService inspection,
        OperationalPolicyService policy) {
        _prebuildService = prebuildService;
        _options = options.Value;
        _maintenance = maintenance;
        _auditMaintenance = auditMaintenance;
        _inspection = inspection;
        _policy = policy;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_options.IsEnabled) {
            Logger.Warn("分表预建计划未启用，托管服务退出。");
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var window = (await _policy.ReadAsync(stoppingToken)).PrebuildAheadHours;
                if (!_options.DryRun) {
                    await _maintenance.ExecuteAsync(stoppingToken, window);
                    await _auditMaintenance.ExecuteAsync(window, stoppingToken);
                }
                var plan = await _prebuildService.BuildPlanAsync(stoppingToken, window);
                await _inspection.InspectAsync(stoppingToken);
                Logger.Info("分表预建窗口复核完成：DryRun={DryRun}, PlannedCount={PlannedCount}, MissingCount={MissingCount}", plan.IsDryRun, plan.PlannedPhysicalTables.Count, plan.MissingPhysicalTables.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { Logger.Error(ex, "分表预建计划托管服务发生异常。"); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
