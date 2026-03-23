namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询聚合指标快照。
/// </summary>
public sealed record SlowQueryMetric(
    string SqlFingerprint,
    string SampleSql,
    int CallCount,
    int TotalAffectedRows,
    decimal ErrorRatePercent,
    decimal TimeoutRatePercent,
    int DeadlockCount,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    int? LockWaitCount);
