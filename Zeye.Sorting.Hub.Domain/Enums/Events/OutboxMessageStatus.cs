using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Events;

/// <summary>
/// Outbox 消息状态枚举。
/// </summary>
public enum OutboxMessageStatus {
    /// <summary>
    /// 待处理。
    /// </summary>
    [Description("待处理")]
    Pending = 1,

    /// <summary>
    /// 处理中。
    /// </summary>
    [Description("处理中")]
    Processing = 2,

    /// <summary>
    /// 已成功。
    /// </summary>
    [Description("已成功")]
    Succeeded = 3,

    /// <summary>
    /// 已失败。
    /// </summary>
    [Description("已失败")]
    Failed = 4,

    /// <summary>
    /// 已进入死信。
    /// </summary>
    [Description("已进入死信")]
    DeadLettered = 5
}
