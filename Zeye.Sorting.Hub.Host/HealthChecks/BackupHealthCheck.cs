using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 备份治理健康检查。
/// </summary>
public sealed class BackupHealthCheck : IHealthCheck {
    /// <summary>
    /// 备份校验服务。
    /// </summary>
    private readonly BackupVerificationService _backupVerificationService;

    /// <summary>
    /// 初始化备份治理健康检查。
    /// </summary>
    /// <param name="backupVerificationService">备份校验服务。</param>
    public BackupHealthCheck(BackupVerificationService backupVerificationService) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var record = _backupVerificationService.GetLastExecutionRecord();
        var data = BuildHealthData(record);
        if (record is null) {
            return Task.FromResult(HealthCheckResult.Degraded("备份治理尚未生成执行记录。", data: data));
        }

        if (!record.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("备份治理未启用。", data: data));
        }

        if (record.Status == BackupExecutionRecord.FailedStatus || !record.HasBackupFile || !record.IsBackupFileFresh) {
            return Task.FromResult(HealthCheckResult.Degraded(record.Summary, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(record.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="record">执行记录。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(BackupExecutionRecord? record) {
        var data = new Dictionary<string, object> {
            ["hasExecutionRecord"] = record is not null
        };
        if (record is null) {
            return data;
        }

        data["recordedAtLocal"] = record.RecordedAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
        data["status"] = record.Status;
        data["provider"] = record.ProviderName;
        data["database"] = record.DatabaseName;
        data["isDryRun"] = record.IsDryRun;
        data["hasBackupFile"] = record.HasBackupFile;
        data["isBackupFileFresh"] = record.IsBackupFileFresh;
        if (record.VerifiedBackupAtLocal.HasValue) {
            data["verifiedBackupAtLocal"] = record.VerifiedBackupAtLocal.Value.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
        }

        if (!string.IsNullOrWhiteSpace(record.VerifiedBackupFilePath)) {
            data["verifiedBackupFilePath"] = record.VerifiedBackupFilePath;
        }

        if (!string.IsNullOrWhiteSpace(record.RestoreRunbookPath)) {
            data["restoreRunbookPath"] = record.RestoreRunbookPath;
        }

        if (!string.IsNullOrWhiteSpace(record.DrillRecordPath)) {
            data["drillRecordPath"] = record.DrillRecordPath;
        }

        return data;
    }
}
