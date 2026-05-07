using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 数据保留治理健康检查。
/// </summary>
public sealed class DataRetentionHealthCheck : IHealthCheck {
    /// <summary>
    /// 数据保留治理执行器。
    /// </summary>
    private readonly DataRetentionExecutor _dataRetentionExecutor;

    /// <summary>
    /// 初始化数据保留治理健康检查。
    /// </summary>
    /// <param name="dataRetentionExecutor">数据保留治理执行器。</param>
    public DataRetentionHealthCheck(DataRetentionExecutor dataRetentionExecutor) {
        _dataRetentionExecutor = dataRetentionExecutor;
    }

    /// <summary>
    /// 执行健康检查。
    /// </summary>
    /// <param name="context">健康检查上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康检查结果。</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var record = _dataRetentionExecutor.GetLatestRecord();
        var data = BuildHealthData(record);
        if (record is null) {
            return Task.FromResult(HealthCheckResult.Degraded("数据保留治理尚未生成状态记录。", data: data));
        }

        if (!record.IsEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("数据保留治理未启用。", data: data));
        }

        if (string.Equals(record.Status, DataRetentionAuditRecord.FailedStatus, StringComparison.Ordinal)) {
            return Task.FromResult(HealthCheckResult.Unhealthy("数据保留治理执行失败。", data: data));
        }

        if (record.TotalCandidateCount > 0) {
            return Task.FromResult(HealthCheckResult.Degraded(record.Summary, data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(record.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="record">执行记录。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(DataRetentionAuditRecord? record) {
        var data = new Dictionary<string, object> {
            ["hasRecord"] = record is not null
        };
        if (record is null) {
            return data;
        }

        data["recordedAtLocal"] = record.RecordedAtLocal.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
        data["status"] = record.Status;
        data["isEnabled"] = record.IsEnabled;
        data["isDryRun"] = record.IsDryRun;
        data["batchSize"] = record.BatchSize;
        data["policyCount"] = record.PolicyCount;
        data["totalCandidateCount"] = record.TotalCandidateCount;
        data["summary"] = record.Summary;
        foreach (var candidateCount in record.CandidateCounts) {
            data[$"candidateCount.{candidateCount.Key}"] = candidateCount.Value;
        }

        if (!string.IsNullOrWhiteSpace(record.FailureMessage)) {
            data["failureMessage"] = record.FailureMessage!;
        }

        return data;
    }
}
