namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询告警状态跟踪项。
/// </summary>
internal sealed class AlertTrackingState {
    /// <summary>
    /// 连续触发窗口数。
    /// </summary>
    public int ConsecutiveTriggeredWindows { get; set; }

    /// <summary>
    /// 连续恢复窗口数。
    /// </summary>
    public int ConsecutiveRecoveredWindows { get; set; }

    /// <summary>
    /// 当前是否处于激活告警状态。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 最近一次通知时间。
    /// </summary>
    public DateTime? LastNotifiedTime { get; set; }

    /// <summary>
    /// 最近一次观测时间。
    /// </summary>
    public DateTime LastSeenTime { get; set; }
}
