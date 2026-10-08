namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询画像快照。
/// </summary>
public sealed record SlowQueryProfileSnapshot(
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
    DateTime LastOccurredAtLocal) {
    /// <summary>最新一次采样的追踪与分类信息。</summary>
    public SlowQueryObservation Observation { get; init; } = new();
    /// <summary>被取消的调用数量。</summary>
    public int CanceledCount { get; init; }
    /// <summary>提前释放读取器的次数。</summary>
    public int PartialReadCount { get; init; }
    /// <summary>平均数据库执行耗时。</summary>
    public decimal AverageExecuteMilliseconds { get; init; }
    /// <summary>平均提供器读调用耗时。</summary>
    public decimal AverageReadMilliseconds { get; init; }
    /// <summary>平均消费方耗时，包含物化与处理。</summary>
    public decimal AverageConsumerMilliseconds { get; init; }
    /// <summary>平均连接等待耗时。</summary>
    public decimal AverageConnectionMilliseconds { get; init; }
    /// <summary>累计读取行数。</summary>
    public long TotalRowsRead { get; init; }
}
