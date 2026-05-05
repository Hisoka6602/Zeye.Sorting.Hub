namespace Zeye.Sorting.Hub.Contracts.Models.DataGovernance;

/// <summary>
/// 归档任务列表查询请求合同。
/// </summary>
public sealed record ArchiveTaskListRequest {
    /// <summary>
    /// 页码。
    /// 可填写范围：1~10000。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 页大小。
    /// 可填写范围：1~200。
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 状态过滤。
    /// 可填写范围：Pending / Running / Completed / Failed。
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// 类型过滤。
    /// 可填写范围：WebRequestAuditLogHistory。
    /// </summary>
    public string? TaskType { get; init; }
}
