namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询告警通知。
/// </summary>
public sealed record SlowQueryAlertNotification(
    string SqlFingerprint,
    string AlertType,
    string Message,
    bool IsRecovery,
    DateTime TriggeredTime);
