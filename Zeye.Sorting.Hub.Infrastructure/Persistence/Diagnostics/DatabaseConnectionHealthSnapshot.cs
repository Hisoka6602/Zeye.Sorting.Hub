namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// 数据库连接诊断快照。
/// </summary>
public sealed record class DatabaseConnectionHealthSnapshot {
    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// 数据库名称。
    /// </summary>
    public required string Database { get; init; }

    /// <summary>
    /// 最近一次探测时间（本地时间语义）。
    /// </summary>
    public required DateTime CheckedAtLocal { get; init; }

    /// <summary>
    /// 最近一次探测耗时（毫秒）。
    /// </summary>
    public required long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 当前连续失败次数。
    /// </summary>
    public required int ConsecutiveFailureCount { get; init; }

    /// <summary>
    /// 当前连续成功次数。
    /// </summary>
    public required int ConsecutiveSuccessCount { get; init; }

    /// <summary>
    /// 最近一次探测是否成功。
    /// </summary>
    public required bool IsProbeSucceeded { get; init; }

    /// <summary>
    /// 是否处于恢复观察期。
    /// </summary>
    public required bool IsRecoveryPending { get; init; }

    /// <summary>
    /// 最近一次失败信息。
    /// </summary>
    public string? FailureMessage { get; init; }
}
