using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Idempotency;

/// <summary>
/// 幂等记录状态枚举。
/// </summary>
public enum IdempotencyRecordStatus {
    /// <summary>
    /// 待处理。
    /// </summary>
    [Description("待处理")]
    Pending = 1,

    /// <summary>
    /// 已完成。
    /// </summary>
    [Description("已完成")]
    Completed = 2,

    /// <summary>
    /// 已拒绝。
    /// </summary>
    [Description("已拒绝")]
    Rejected = 3,

    /// <summary>
    /// 已失败。
    /// </summary>
    [Description("已失败")]
    Failed = 4
}
