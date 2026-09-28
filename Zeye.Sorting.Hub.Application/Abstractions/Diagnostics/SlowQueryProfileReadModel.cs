namespace Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;

/// <summary>
/// 慢查询画像读模型。
/// </summary>
public sealed record SlowQueryProfileReadModel(
    string Fingerprint,
    string NormalizedSql,
    string SampleSql,
    int CallCount,
    decimal AverageElapsedMilliseconds,
    decimal P95Milliseconds,
    decimal P99Milliseconds,
    decimal MaxMilliseconds,
    int TimeoutCount,
    int ErrorCount,
    int DeadlockCount,
    int TotalAffectedRows,
    DateTime WindowStartedAtLocal,
    DateTime WindowEndedAtLocal,
    DateTime LastOccurredAtLocal);
