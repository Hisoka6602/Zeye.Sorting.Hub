using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 分表治理健康检查。
/// </summary>
public sealed class ShardingGovernanceHealthCheck : IHealthCheck {
    /// <summary>
    /// 分表巡检服务。
    /// </summary>
    private readonly ShardingTableInspectionService _inspectionService;

    /// <summary>
    /// 分表预建计划服务。
    /// </summary>
    private readonly ShardingTablePrebuildService _prebuildService;

    /// <summary>
    /// 初始化分表治理健康检查。
    /// </summary>
    /// <param name="inspectionService">分表巡检服务。</param>
    /// <param name="prebuildService">分表预建计划服务。</param>
    public ShardingGovernanceHealthCheck(
        ShardingTableInspectionService inspectionService,
        ShardingTablePrebuildService prebuildService) {
        _inspectionService = inspectionService;
        _prebuildService = prebuildService;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var report = _inspectionService.GetLastReport();
        var plan = _prebuildService.GetLastPlan();
        var data = BuildHealthData(report, plan);
        if (report is null) {
            return Task.FromResult(HealthCheckResult.Degraded("分表巡检尚未生成报告。", data: data));
        }

        if (!report.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("分表运行期巡检未启用。", data: data));
        }

        if (!report.IsHealthy) {
            return Task.FromResult(HealthCheckResult.Unhealthy("分表治理巡检发现缺表、缺索引或容量风险。", data: data));
        }

        if (plan is not null && plan.MissingPhysicalTables.Count > 0) {
            return Task.FromResult(HealthCheckResult.Degraded("分表预建计划发现未来窗口存在缺失物理表。", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("分表治理状态正常。", data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="report">巡检报告。</param>
    /// <param name="plan">预建计划。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(ShardingInspectionReport? report, ShardingPrebuildPlan? plan) {
        var data = new Dictionary<string, object> {
            ["hasInspectionReport"] = report is not null,
            ["hasPrebuildPlan"] = plan is not null
        };
        if (report is not null) {
            data["checkedAtLocal"] = report.CheckedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
            data["provider"] = report.ProviderName;
            data["missingPhysicalTableCount"] = report.MissingPhysicalTables.Count;
            data["missingIndexCount"] = report.MissingIndexes.Count;
            data["capacityWarningCount"] = report.CapacityWarnings.Count;
            data["pairWarningCount"] = report.WebRequestAuditLogPairWarnings.Count;
            data["isInspectionHealthy"] = report.IsHealthy;
        }

        if (plan is not null) {
            data["prebuildGeneratedAtLocal"] = plan.GeneratedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
            data["prebuildDryRun"] = plan.IsDryRun;
            data["prebuildPlannedTableCount"] = plan.PlannedPhysicalTables.Count;
            data["prebuildMissingTableCount"] = plan.MissingPhysicalTables.Count;
        }

        return data;
    }
}
