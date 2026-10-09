using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

    /// <summary>分表巡检周期。</summary>
    private readonly ShardingRuntimeInspectionOptions _inspectionOptions;

    /// <summary>是否启用周期预建。</summary>
    private readonly ShardingPrebuildOptions _prebuildOptions;

    /// <summary>用于本地快照有效期判定的时间来源。</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 初始化分表治理健康检查。
    /// </summary>
    /// <param name="inspectionService">分表巡检服务。</param>
    /// <param name="prebuildService">分表预建计划服务。</param>
    /// <param name="inspectionOptions">分表巡检周期。</param>
    /// <param name="prebuildOptions">分表预建启用策略。</param>
    /// <param name="timeProvider">本地快照时间来源。</param>
    public ShardingGovernanceHealthCheck(
        ShardingTableInspectionService inspectionService,
        ShardingTablePrebuildService prebuildService,
        IOptions<ShardingRuntimeInspectionOptions>? inspectionOptions = null,
        IOptions<ShardingPrebuildOptions>? prebuildOptions = null,
        TimeProvider? timeProvider = null) {
        _inspectionService = inspectionService;
        _prebuildService = prebuildService;
        _inspectionOptions = inspectionOptions?.Value ?? new ShardingRuntimeInspectionOptions();
        _prebuildOptions = prebuildOptions?.Value ?? new ShardingPrebuildOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        if (HealthSnapshotFreshness.IsStale(report.CheckedAtLocal, TimeSpan.FromMinutes(_inspectionOptions.InspectionIntervalMinutes * 2L + 5), _timeProvider)) {
            return Task.FromResult(HealthCheckResult.Degraded("分表巡检结果已过期，后台任务可能已停滞。", data: data));
        }
        if (_prebuildOptions.IsEnabled && plan is not null && HealthSnapshotFreshness.IsStale(plan.GeneratedAtLocal, TimeSpan.FromMinutes(5), _timeProvider)) {
            return Task.FromResult(HealthCheckResult.Degraded("分表预建计划已过期，跨周期建表可能未持续执行。", data: data));
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
    private static System.Collections.Generic.Dictionary<string, object> BuildHealthData(ShardingInspectionReport? report, ShardingPrebuildPlan? plan) {
        var data = new Dictionary<string, object> {
            ["hasInspectionReport"] = report is not null,
            ["hasPrebuildPlan"] = plan is not null
        };
        if (report is not null) {
            data["checkedAtLocal"] = report.CheckedAtLocal.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            data["provider"] = report.ProviderName;
            data["missingPhysicalTableCount"] = report.MissingPhysicalTables.Count;
            data["missingIndexCount"] = report.MissingIndexes.Count;
            data["capacityWarningCount"] = report.CapacityWarnings.Count;
            data["pairWarningCount"] = report.WebRequestAuditLogPairWarnings.Count;
            data["isInspectionHealthy"] = report.IsHealthy;
        }

        if (plan is not null) {
            data["prebuildGeneratedAtLocal"] = plan.GeneratedAtLocal.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            data["prebuildDryRun"] = plan.IsDryRun;
            data["prebuildPlannedTableCount"] = plan.PlannedPhysicalTables.Count;
            data["prebuildMissingTableCount"] = plan.MissingPhysicalTables.Count;
        }

        return data;
    }
}
