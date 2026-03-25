using NLog;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 默认执行计划探针：结构化输出 unavailable/available 状态，并发出观测指标。
/// </summary>
public sealed class LoggingOnlyExecutionPlanRegressionProbe : IProviderAwareExecutionPlanRegressionProbe {
    /// <summary>
    /// NLog 静态日志器实例，用于输出执行计划探针评估结果。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 字段：_observability。
    /// </summary>
    private readonly IAutoTuningObservability _observability;

    /// <summary>
    /// 初始化 logging-only 探针。
    /// </summary>
    public LoggingOnlyExecutionPlanRegressionProbe(
        IAutoTuningObservability observability) {
        _observability = observability;
    }

    /// <summary>
    /// 兼容入口：由 providerName + fingerprint 评估。
    /// </summary>
    public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
        var request = new ExecutionPlanProbeRequest(providerName, sqlFingerprint);
        return Evaluate(request);
    }

    /// <summary>
    /// provider-aware 评估入口。
    /// </summary>
    public PlanRegressionSnapshot Evaluate(in ExecutionPlanProbeRequest request) {
        var normalizedProvider = NormalizeParameter(request.ProviderName);
        var normalizedFingerprint = NormalizeParameter(request.SqlFingerprint);
        var snapshot = BuildSnapshot(normalizedProvider, normalizedFingerprint);
        _observability.EmitMetric(
            "autotuning.plan_probe.evaluation",
            1d,
            new Dictionary<string, string> {
                ["provider"] = normalizedProvider,
                ["fingerprint"] = normalizedFingerprint,
                ["available"] = snapshot.IsAvailable ? "true" : "false",
                ["unavailable_reason"] = snapshot.UnavailableReason
            });
        _observability.EmitEvent(
            "autotuning.plan_probe.result",
            snapshot.IsAvailable ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Warning,
            snapshot.Summary,
            new Dictionary<string, string> {
                ["provider"] = normalizedProvider,
                ["fingerprint"] = normalizedFingerprint,
                ["available"] = snapshot.IsAvailable ? "true" : "false",
                ["unavailable_reason"] = snapshot.UnavailableReason
            });
        Logger.Info(
            "执行计划回退探针评估：Provider={Provider}, Fingerprint={Fingerprint}, IsAvailable={IsAvailable}, IsRegressed={IsRegressed}, UnavailableReason={UnavailableReason}, Summary={Summary}",
            normalizedProvider,
            normalizedFingerprint,
            snapshot.IsAvailable,
            snapshot.IsRegressed,
            snapshot.UnavailableReason,
            snapshot.Summary);
        return snapshot;
    }

    /// <summary>
    /// 规范化探针输入参数：空值回退为 unknown，非空值执行首尾空白裁剪。
    /// </summary>
    /// <param name="value">原始参数值。</param>
    /// <returns>可用于日志与标签输出的规范化参数。</returns>
    private static string NormalizeParameter(string? value) {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
    }

    /// <summary>
    /// 构建执行计划回退快照。
    /// 步骤说明：
    /// 1. 优先匹配“查询失败/权限不足”等不可用场景并返回 unavailable；
    /// 2. 再匹配“可用且回归/可用且通过”场景并返回 available；
    /// 3. 最后统一回退为“方言不支持”不可用场景。
    /// </summary>
    /// <param name="providerName">数据库提供程序名称。</param>
    /// <param name="sqlFingerprint">SQL 指纹。</param>
    /// <returns>执行计划回退快照。</returns>
    private static PlanRegressionSnapshot BuildSnapshot(string providerName, string sqlFingerprint) {
        if (string.Equals(sqlFingerprint, "plan-probe-query-failed", StringComparison.OrdinalIgnoreCase)) {
            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={sqlFingerprint}, plan regression unavailable(query failed)",
                UnavailableReason: AutoTuningUnavailableReason.QueryFailed.ToTagValue());
        }

        if (string.Equals(sqlFingerprint, "plan-probe-permission-denied", StringComparison.OrdinalIgnoreCase)) {
            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={sqlFingerprint}, plan regression unavailable(permission denied)",
                UnavailableReason: AutoTuningUnavailableReason.PermissionDenied.ToTagValue());
        }

        if (string.Equals(sqlFingerprint, "plan-probe-available-regressed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sqlFingerprint, "plan-probe-available-pass", StringComparison.OrdinalIgnoreCase)) {
            var regressed = string.Equals(sqlFingerprint, "plan-probe-available-regressed", StringComparison.OrdinalIgnoreCase);
            return new PlanRegressionSnapshot(
                IsAvailable: true,
                IsRegressed: regressed,
                Summary: $"fingerprint={sqlFingerprint}, provider={providerName}, simulated plan regression sampled",
                UnavailableReason: AutoTuningUnavailableReason.None.ToTagValue());
        }

        return new PlanRegressionSnapshot(
            IsAvailable: false,
            IsRegressed: false,
            Summary: $"fingerprint={sqlFingerprint}, provider={providerName}, plan regression unavailable(dialect not supported)",
            UnavailableReason: AutoTuningUnavailableReason.DialectNotSupported.ToTagValue());
    }
}
