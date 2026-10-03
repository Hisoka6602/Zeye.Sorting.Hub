using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
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

    /// <summary>备份最大年龄及轮询间隔。</summary>
    private readonly BackupOptions _options;

    /// <summary>用于快照有效期判定的时间来源。</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 初始化备份治理健康检查。
    /// </summary>
    /// <param name="backupVerificationService">备份校验服务。</param>
    /// <param name="options">备份年龄与轮询预算。</param>
    /// <param name="timeProvider">本地快照时间来源。</param>
    public BackupHealthCheck(BackupVerificationService backupVerificationService, IOptions<BackupOptions>? options = null, TimeProvider? timeProvider = null) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
        _options = options?.Value ?? new BackupOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
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

        if (HealthSnapshotFreshness.IsStale(record.RecordedAtLocal, TimeSpan.FromMinutes(_options.PollIntervalMinutes * 2L + 5), _timeProvider)) {
            return Task.FromResult(HealthCheckResult.Degraded("备份治理结果已过期，后台任务可能已停滞。", data: data));
        }
        if (record.VerifiedBackupAtLocal is DateTime verifiedAt && HealthSnapshotFreshness.IsStale(verifiedAt, TimeSpan.FromHours(_options.MaxAllowedBackupAgeHours), _timeProvider)) {
            return Task.FromResult(HealthCheckResult.Degraded("最新备份已超出允许年龄，旧的成功校验不再有效。", data: data));
        }
        if (record.HasBackupFile && !File.Exists(record.VerifiedBackupFilePath)) {
            return Task.FromResult(HealthCheckResult.Degraded("最近验证的备份文件已不存在。", data: data));
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
