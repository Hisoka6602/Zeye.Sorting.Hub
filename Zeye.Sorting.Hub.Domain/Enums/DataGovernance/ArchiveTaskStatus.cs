using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.DataGovernance;

/// <summary>
/// 归档任务状态枚举。
/// </summary>
public enum ArchiveTaskStatus {
    /// <summary>
    /// 待执行。
    /// </summary>
    [Description("待执行")]
    Pending = 1,

    /// <summary>
    /// 执行中。
    /// </summary>
    [Description("执行中")]
    Running = 2,

    /// <summary>
    /// 已完成。
    /// </summary>
    [Description("已完成")]
    Completed = 3,

    /// <summary>
    /// 已失败。
    /// </summary>
    [Description("已失败")]
    Failed = 4
}
