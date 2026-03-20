namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// 管理端触发过期包裹清理的请求合同（治理型接口）。
/// </summary>
public sealed record ParcelCleanupExpiredRequest {
    /// <summary>
    /// 创建时间上界（本地时间字符串）。
    /// 早于此时间创建的包裹视为过期候选，将纳入计划清理范围。
    /// 格式支持：yyyy-MM-dd / yyyy-MM-dd HH:mm:ss / yyyy-MM-ddTHH:mm:ss 等，不允许 UTC 或 offset 表达。
    /// </summary>
    public required string CreatedBefore { get; init; }
}
