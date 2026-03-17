using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums;

/// <summary>
/// AutoTuningUnavailableReason 枚举。
/// </summary>
public enum AutoTuningUnavailableReason {
    /// <summary>
    /// 未命中不可用原因。
    /// </summary>
    [Description("无")]
    None = 0,
    /// <summary>
    /// 已采样但无需标记不可用。
    /// </summary>
    [Description("已采样")]
    Sampled = 1,
    /// <summary>
    /// 基线与当前窗口均不可用。
    /// </summary>
    [Description("基线与当前窗口不可用")]
    BaselineAndCurrentUnavailable = 2,
    /// <summary>
    /// 基线窗口不可用。
    /// </summary>
    [Description("基线窗口不可用")]
    BaselineUnavailable = 3,
    /// <summary>
    /// 当前窗口不可用。
    /// </summary>
    [Description("当前窗口不可用")]
    CurrentUnavailable = 4,
    /// <summary>
    /// 指标窗口缺失。
    /// </summary>
    [Description("指标窗口缺失")]
    MetricWindowMiss = 5,
    /// <summary>
    /// 执行计划回归探针被禁用。
    /// </summary>
    [Description("计划探针被禁用")]
    PlanProbeDisabled = 6,
    /// <summary>
    /// 执行计划回归探针因采样策略跳过。
    /// </summary>
    [Description("计划探针采样跳过")]
    PlanProbeSamplingSkipped = 7,
    /// <summary>
    /// 权限不足导致不可用。
    /// </summary>
    [Description("权限不足")]
    PermissionDenied = 8,
    /// <summary>
    /// 查询执行失败导致不可用。
    /// </summary>
    [Description("查询失败")]
    QueryFailed = 9,
    /// <summary>
    /// 当前数据库方言不支持。
    /// </summary>
    [Description("数据库方言不支持")]
    DialectNotSupported = 10
}

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
