namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 待回滚动作快照，用于自动验证回归后执行补偿回滚。
/// </summary>
internal sealed record PendingRollbackAction(
    string ActionId,
    string Fingerprint,
    string RollbackSql,
    string TableKey,
    DateTime CreatedTime,
    int CreatedCycle,
    double BaselineP95Milliseconds,
    double BaselineP99Milliseconds,
    decimal BaselineErrorRatePercent,
    decimal BaselineTimeoutRatePercent,
    int BaselineDeadlockCount,
    int? BaselineLockWaitCount);
