namespace Zeye.Sorting.Hub.Domain.Enums;

/// <summary>
/// <see cref="AutoTuningUnavailableReason"/> 扩展方法。
/// </summary>
public static class AutoTuningUnavailableReasonExtensions {
    /// <summary>
    /// 将不可用原因枚举映射为稳定的 metrics/log 标签值；当出现未覆盖的新枚举值时返回 <c>unknown</c>。
    /// </summary>
    public static string ToTagValue(this AutoTuningUnavailableReason reason) {
        return reason switch {
            AutoTuningUnavailableReason.None => "none",
            AutoTuningUnavailableReason.Sampled => "sampled",
            AutoTuningUnavailableReason.BaselineAndCurrentUnavailable => "baseline-and-current-unavailable",
            AutoTuningUnavailableReason.BaselineUnavailable => "baseline-unavailable",
            AutoTuningUnavailableReason.CurrentUnavailable => "current-unavailable",
            AutoTuningUnavailableReason.MetricWindowMiss => "metric-window-miss",
            AutoTuningUnavailableReason.PlanProbeDisabled => "plan-probe-disabled",
            AutoTuningUnavailableReason.PlanProbeSamplingSkipped => "plan-probe-sampling-skipped",
            AutoTuningUnavailableReason.PermissionDenied => "permission-denied",
            AutoTuningUnavailableReason.QueryFailed => "query-failed",
            AutoTuningUnavailableReason.DialectNotSupported => "dialect-not-supported",
            _ => "unknown"
        };
    }
}
