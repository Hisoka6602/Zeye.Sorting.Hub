using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 备份治理健康检查。
/// </summary>
public sealed class BackupHealthCheck : IHealthCheck {
    /// <summary>
    /// 备份治理验证服务。
    /// </summary>
    private readonly BackupVerificationService _backupVerificationService;

    /// <summary>
    /// 初始化备份治理健康检查。
    /// </summary>
    /// <param name="backupVerificationService">备份治理验证服务。</param>
    public BackupHealthCheck(BackupVerificationService backupVerificationService) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
    }

    /// <summary>
    /// 执行健康检查。
    /// </summary>
    /// <param name="context">健康检查上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康检查结果。</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var plan = _backupVerificationService.GetLatestPlan();
        var record = _backupVerificationService.GetLatestExecutionRecord();
        var data = BuildHealthData(plan, record);
        if (record is null) {
            return Task.FromResult(HealthCheckResult.Degraded("备份治理尚未生成状态记录。", data: data));
        }

        if (!record.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("备份治理未启用。", data: data));
        }

        if (string.Equals(record.Status, BackupExecutionRecord.FailedStatus, StringComparison.Ordinal)
            || string.Equals(record.Status, BackupExecutionRecord.DegradedStatus, StringComparison.Ordinal)) {
            return Task.FromResult(HealthCheckResult.Degraded(record.Summary, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(record.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="plan">备份计划。</param>
    /// <param name="record">执行记录。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(BackupPlan? plan, BackupExecutionRecord? record) {
        var data = new Dictionary<string, object> {
            ["hasPlan"] = plan is not null,
            ["hasExecutionRecord"] = record is not null
        };
        if (plan is not null) {
            data["generatedAtLocal"] = plan.GeneratedAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
            data["provider"] = plan.ProviderName;
            data["databaseName"] = plan.DatabaseName;
            data["backupDirectoryPath"] = plan.BackupDirectoryPath;
            data["expectedBackupFilePath"] = plan.ExpectedBackupFilePath;
            data["expectedBackupCutoffAtLocal"] = plan.ExpectedBackupCutoffAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
            data["restoreDrillDirectoryPath"] = plan.RestoreDrillDirectoryPath;
            if (!string.IsNullOrWhiteSpace(plan.LatestBackupArtifactPath)) {
                data["latestBackupArtifactPath"] = plan.LatestBackupArtifactPath!;
            }

            if (plan.LatestBackupArtifactTimeLocal.HasValue) {
                data["latestBackupArtifactTimeLocal"] = plan.LatestBackupArtifactTimeLocal.Value.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
            }

            if (!string.IsNullOrWhiteSpace(plan.LatestRestoreDrillRecordPath)) {
                data["latestRestoreDrillRecordPath"] = plan.LatestRestoreDrillRecordPath!;
            }
        }

        if (record is not null) {
            data["recordedAtLocal"] = record.RecordedAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
            data["status"] = record.Status;
            data["isDryRun"] = record.IsDryRun;
            data["hasRecentBackupArtifact"] = record.HasRecentBackupArtifact;
            data["hasRestoreDrillRecord"] = record.HasRestoreDrillRecord;
            data["summary"] = record.Summary;
            if (!string.IsNullOrWhiteSpace(record.FailureMessage)) {
                data["failureMessage"] = record.FailureMessage!;
            }
        }

        return data;
    }
}
