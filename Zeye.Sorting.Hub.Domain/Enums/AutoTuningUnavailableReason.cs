namespace Zeye.Sorting.Hub.Domain.Enums;

public enum AutoTuningUnavailableReason {
    None = 0,
    Sampled = 1,
    BaselineAndCurrentUnavailable = 2,
    BaselineUnavailable = 3,
    CurrentUnavailable = 4,
    MetricWindowMiss = 5,
    PlanProbeDisabled = 6,
    PlanProbeSamplingSkipped = 7,
    PermissionDenied = 8,
    QueryFailed = 9,
    DialectNotSupported = 10
}

public static class AutoTuningUnavailableReasonExtensions {
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
