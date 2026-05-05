using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 数据库连接详细健康检查。
/// </summary>
public sealed class DatabaseConnectionDetailedHealthCheck : IHealthCheck {
    /// <summary>
    /// 数据库连接诊断服务。
    /// </summary>
    private readonly IDatabaseConnectionDiagnostics _databaseConnectionDiagnostics;

    /// <summary>
    /// 诊断配置。
    /// </summary>
    private readonly DatabaseConnectionDiagnosticsOptions _options;

    /// <summary>
    /// 初始化 <see cref="DatabaseConnectionDetailedHealthCheck"/>。
    /// </summary>
    /// <param name="databaseConnectionDiagnostics">数据库连接诊断服务。</param>
    /// <param name="options">诊断配置。</param>
    public DatabaseConnectionDetailedHealthCheck(
        IDatabaseConnectionDiagnostics databaseConnectionDiagnostics,
        IOptions<DatabaseConnectionDiagnosticsOptions> options) {
        _databaseConnectionDiagnostics = databaseConnectionDiagnostics;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var snapshot = await _databaseConnectionDiagnostics.ProbeAsync(cancellationToken);
        var data = BuildHealthData(snapshot);
        if (!snapshot.IsProbeSucceeded) {
            return snapshot.ConsecutiveFailureCount >= _options.FailureThreshold
                ? HealthCheckResult.Unhealthy("数据库连接连续失败已达到告警阈值。", data: data)
                : HealthCheckResult.Degraded("数据库连接探测失败，但尚未达到阻断阈值。", data: data);
        }

        return snapshot.IsRecoveryPending
            ? HealthCheckResult.Degraded("数据库连接已恢复，但仍处于恢复观察期。", data: data)
            : HealthCheckResult.Healthy("数据库连接正常。", data: data);
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="snapshot">诊断快照。</param>
    /// <returns>附加数据字典。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(DatabaseConnectionHealthSnapshot snapshot) {
        var data = new Dictionary<string, object> {
            ["provider"] = snapshot.Provider,
            ["database"] = snapshot.Database,
            ["checkedAtLocal"] = snapshot.CheckedAtLocal.ToString("yyyy-MM-dd HH:mm:ss"),
            ["elapsedMilliseconds"] = snapshot.ElapsedMilliseconds,
            ["consecutiveFailureCount"] = snapshot.ConsecutiveFailureCount,
            ["consecutiveSuccessCount"] = snapshot.ConsecutiveSuccessCount,
            ["isProbeSucceeded"] = snapshot.IsProbeSucceeded,
            ["isRecoveryPending"] = snapshot.IsRecoveryPending
        };
        if (!string.IsNullOrWhiteSpace(snapshot.FailureMessage)) {
            data["failureMessage"] = snapshot.FailureMessage;
        }

        return data;
    }
}
