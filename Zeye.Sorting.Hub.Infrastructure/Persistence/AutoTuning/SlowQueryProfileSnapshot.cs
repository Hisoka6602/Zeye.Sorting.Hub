namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询画像快照。
/// </summary>
public sealed record SlowQueryProfileSnapshot(
    string Fingerprint,
    string NormalizedSql,
    string SampleSql,
    int CallCount,
    double AverageElapsedMilliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    int TimeoutCount,
    int ErrorCount,
    int DeadlockCount,
    int TotalAffectedRows,
    DateTime WindowStartedAtLocal,
    DateTime WindowEndedAtLocal,
    DateTime LastOccurredAtLocal);
