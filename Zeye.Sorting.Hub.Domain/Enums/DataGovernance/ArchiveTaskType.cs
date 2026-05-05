using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.DataGovernance;

/// <summary>
/// 归档任务类型枚举。
/// </summary>
public enum ArchiveTaskType {
    /// <summary>
    /// Web 请求审计日志历史数据归档演练。
    /// </summary>
    [Description("Web 请求审计日志历史数据归档")]
    WebRequestAuditLogHistory = 1
}
