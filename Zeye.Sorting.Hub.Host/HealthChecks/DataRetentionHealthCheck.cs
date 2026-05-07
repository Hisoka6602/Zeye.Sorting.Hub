using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 数据保留治理健康检查。
/// </summary>
public sealed class DataRetentionHealthCheck : IHealthCheck {
    /// <summary>
    /// 数据保留执行器。
    /// </summary>
    private readonly DataRetentionExecutor _executor;

    /// <summary>
    /// 初始化数据保留治理健康检查。
    /// </summary>
    /// <param name="executor">数据保留执行器。</param>
    public DataRetentionHealthCheck(DataRetentionExecutor executor) {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var record = _executor.GetLastAuditRecord();
        var data = BuildHealthData(record);
        if (record is null) {
            return Task.FromResult(HealthCheckResult.Degraded("数据保留治理尚未生成审计记录。", data: data));
        }

        if (!record.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("数据保留治理未启用。", data: data));
        }

        if (record.FailedPolicyCount > 0) {
            return Task.FromResult(HealthCheckResult.Degraded("数据保留治理存在失败策略。", data: data));
        }

        if (record.Decision == ActionIsolationDecision.BlockedByGuard && record.PlannedCount > 0) {
            return Task.FromResult(HealthCheckResult.Degraded("数据保留治理被危险动作守卫阻断。", data: data));
        }

        if (record.Decision == ActionIsolationDecision.DryRunOnly && record.PlannedCount > 0) {
            return Task.FromResult(HealthCheckResult.Degraded("数据保留治理当前仅执行 dry-run。", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(record.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="record">审计记录。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(DataRetentionAuditRecord? record) {
        var data = new Dictionary<string, object> {
            ["hasAuditRecord"] = record is not null
        };
        if (record is null) {
            return data;
        }

        data["recordedAtLocal"] = record.RecordedAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
        data["status"] = record.Status;
        data["isDryRun"] = record.IsDryRun;
        data["policyCount"] = record.PolicyCount;
        data["plannedCount"] = record.PlannedCount;
        data["executedCount"] = record.ExecutedCount;
        data["failedPolicyCount"] = record.FailedPolicyCount;
        if (record.Decision.HasValue) {
            data["decision"] = record.Decision.Value.ToString();
        }

        return data;
    }
}
