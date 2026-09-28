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
    decimal P95Milliseconds,
    decimal P99Milliseconds,
    decimal MaxMilliseconds,
    int? LockWaitCount);
