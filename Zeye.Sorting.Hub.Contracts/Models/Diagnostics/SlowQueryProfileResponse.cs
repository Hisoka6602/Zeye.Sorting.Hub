namespace Zeye.Sorting.Hub.Contracts.Models.Diagnostics;

/// <summary>
/// 慢查询画像响应合同。
/// </summary>
public sealed record SlowQueryProfileResponse {
    /// <summary>
    /// SQL 指纹。
    /// </summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// 去参数化后的标准 SQL。
    /// </summary>
    public required string NormalizedSql { get; init; }

    /// <summary>
    /// 最近一条样例 SQL。
    /// </summary>
    public required string SampleSql { get; init; }

    /// <summary>
    /// 调用次数。
    /// </summary>
    public int CallCount { get; init; }

    /// <summary>
    /// 平均耗时（毫秒）。
    /// </summary>
    public double AverageElapsedMilliseconds { get; init; }

    /// <summary>
    /// P95 耗时（毫秒）。
    /// </summary>
    public double P95Milliseconds { get; init; }

    /// <summary>
    /// P99 耗时（毫秒）。
    /// </summary>
    public double P99Milliseconds { get; init; }

    /// <summary>
    /// 最大耗时（毫秒）。
    /// </summary>
    public double MaxMilliseconds { get; init; }

    /// <summary>
    /// 超时次数。
    /// </summary>
    public int TimeoutCount { get; init; }

    /// <summary>
    /// 异常次数。
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// 死锁次数。
    /// </summary>
    public int DeadlockCount { get; init; }

    /// <summary>
    /// 累计影响行数。
    /// </summary>
    public int TotalAffectedRows { get; init; }

    /// <summary>
    /// 当前窗口开始时间（本地时间语义）。
    /// </summary>
    public DateTime WindowStartedAtLocal { get; init; }

    /// <summary>
    /// 当前窗口结束时间（本地时间语义）。
    /// </summary>
    public DateTime WindowEndedAtLocal { get; init; }

    /// <summary>
    /// 最近一次出现时间（本地时间语义）。
    /// </summary>
    public DateTime LastOccurredAtLocal { get; init; }
}
