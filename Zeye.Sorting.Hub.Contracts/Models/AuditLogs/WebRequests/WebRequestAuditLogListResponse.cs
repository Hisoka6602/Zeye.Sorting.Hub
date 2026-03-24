namespace Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;

/// <summary>
/// Web 请求审计日志列表分页响应合同。
/// </summary>
public sealed record WebRequestAuditLogListResponse {
    /// <summary>
    /// 当前页列表数据。
    /// </summary>
    public required IReadOnlyList<WebRequestAuditLogListItemResponse> Items { get; init; }

    /// <summary>
    /// 当前页码。
    /// </summary>
    public required int PageNumber { get; init; }

    /// <summary>
    /// 当前页大小。
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// 总记录数。
    /// </summary>
    public required long TotalCount { get; init; }
}
