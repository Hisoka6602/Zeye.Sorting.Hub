namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询分析结果。
/// </summary>
public sealed record SlowQueryAnalysisResult(
    DateTime GeneratedTime,
    int DroppedSamples,
    IReadOnlyList<SlowQueryMetric> Metrics,
    IReadOnlyList<SlowQueryTuningCandidate> TuningCandidates,
    IReadOnlyList<string> ReadOnlySuggestions,
    IReadOnlyList<SlowQuerySuggestionInsight> SuggestionInsights,
    IReadOnlyList<string> Alerts,
    IReadOnlyList<string> RecoveryNotifications,
    IReadOnlyList<SlowQueryAlertNotification> AlertNotifications,
    bool ShouldEmitDailyReport,
    bool ShouldEmitMonthlyReport,
    bool ShouldEmitAnnualDashboard) {
    /// <summary>
    /// 空分析结果。
    /// </summary>
    public static SlowQueryAnalysisResult Empty => new(
        GeneratedTime: DateTime.Now,
        DroppedSamples: 0,
        Metrics: Array.Empty<SlowQueryMetric>(),
        TuningCandidates: Array.Empty<SlowQueryTuningCandidate>(),
        ReadOnlySuggestions: Array.Empty<string>(),
        Alerts: Array.Empty<string>(),
        SuggestionInsights: Array.Empty<SlowQuerySuggestionInsight>(),
        RecoveryNotifications: Array.Empty<string>(),
        AlertNotifications: Array.Empty<SlowQueryAlertNotification>(),
        ShouldEmitDailyReport: false,
        ShouldEmitMonthlyReport: false,
        ShouldEmitAnnualDashboard: false);
}
