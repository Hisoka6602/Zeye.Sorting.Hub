using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 迁移治理健康检查。
/// </summary>
public sealed class MigrationGovernanceHealthCheck : IHealthCheck {
    /// <summary>
    /// 迁移治理状态存储。
    /// </summary>
    private readonly MigrationGovernanceStateStore _migrationGovernanceStateStore;

    /// <summary>
    /// 初始化迁移治理健康检查。
    /// </summary>
    /// <param name="migrationGovernanceStateStore">迁移治理状态存储。</param>
    public MigrationGovernanceHealthCheck(MigrationGovernanceStateStore migrationGovernanceStateStore) {
        _migrationGovernanceStateStore = migrationGovernanceStateStore;
    }

    /// <summary>
    /// 执行健康检查。
    /// </summary>
    /// <param name="context">健康检查上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康检查结果。</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var plan = _migrationGovernanceStateStore.GetLatestPlan();
        var record = _migrationGovernanceStateStore.GetLatestExecutionRecord();
        var data = BuildHealthData(plan, record);
        if (record is null) {
            return Task.FromResult(HealthCheckResult.Degraded("迁移治理尚未生成状态记录。", data: data));
        }

        if (!record.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("迁移治理未启用。", data: data));
        }

        if (string.Equals(record.Status, MigrationExecutionRecord.FailedStatus, StringComparison.Ordinal)) {
            return Task.FromResult(HealthCheckResult.Unhealthy("迁移治理预演或执行失败。", data: data));
        }

        if (record.PendingMigrationCount > 0 && !record.ShouldApplyMigrations) {
            return Task.FromResult(HealthCheckResult.Degraded(record.Summary, data: data));
        }

        if (record.PendingMigrationCount > 0 && string.Equals(record.Status, MigrationExecutionRecord.PreparedStatus, StringComparison.Ordinal)) {
            return Task.FromResult(HealthCheckResult.Degraded("存在待执行迁移，等待数据库初始化阶段完成执行。", data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(record.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <param name="record">执行记录。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(MigrationPlan? plan, MigrationExecutionRecord? record) {
        var data = new Dictionary<string, object> {
            ["hasPlan"] = plan is not null,
            ["hasExecutionRecord"] = record is not null
        };

        if (plan is not null) {
            data["generatedAtLocal"] = plan.GeneratedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
            data["provider"] = plan.ProviderName;
            data["environment"] = plan.EnvironmentName;
            data["allMigrationCount"] = plan.AllMigrations.Count;
            data["appliedMigrationCount"] = plan.AppliedMigrations.Count;
            data["pendingMigrationCount"] = plan.PendingMigrations.Count;
            data["dangerousOperationCount"] = plan.DangerousOperations.Count;
            data["shouldApplyMigrations"] = plan.ShouldApplyMigrations;
            data["isDryRun"] = plan.IsDryRun;
            if (!string.IsNullOrWhiteSpace(plan.SkipReason)) {
                data["skipReason"] = plan.SkipReason!;
            }

            if (!string.IsNullOrWhiteSpace(plan.ArchivedForwardScriptPath)) {
                data["archivedForwardScriptPath"] = plan.ArchivedForwardScriptPath!;
            }

            if (!string.IsNullOrWhiteSpace(plan.ArchivedRollbackScriptPath)) {
                data["archivedRollbackScriptPath"] = plan.ArchivedRollbackScriptPath!;
            }
        }

        if (record is not null) {
            data["recordedAtLocal"] = record.RecordedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
            data["status"] = record.Status;
            data["summary"] = record.Summary;
            data["recordPendingMigrationCount"] = record.PendingMigrationCount;
            data["recordDangerousOperationCount"] = record.DangerousOperationCount;
            if (!string.IsNullOrWhiteSpace(record.FailureMessage)) {
                data["failureMessage"] = record.FailureMessage!;
            }
        }

        return data;
    }
}
