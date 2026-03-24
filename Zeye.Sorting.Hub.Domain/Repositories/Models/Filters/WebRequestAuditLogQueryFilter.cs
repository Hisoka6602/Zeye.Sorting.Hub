namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;

/// <summary>
/// Web 请求审计日志查询过滤参数。
/// </summary>
public sealed record WebRequestAuditLogQueryFilter {
    /// <summary>
    /// 请求开始时间起点（含边界）。
    /// </summary>
    public DateTime? StartedAtStart { get; init; }

    /// <summary>
    /// 请求开始时间终点（含边界）。
    /// </summary>
    public DateTime? StartedAtEnd { get; init; }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool? IsSuccess { get; init; }

    /// <summary>
    /// 调用链追踪 Id。
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// 业务关联 Id。
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// 请求路径关键字。
    /// </summary>
    public string? RequestPathKeyword { get; init; }
}
