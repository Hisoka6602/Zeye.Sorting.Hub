namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// Web 请求审计日志列表查询参数模型。
/// </summary>
internal sealed record WebRequestAuditLogListQueryParameters {
    /// <summary>
    /// 页码（从 1 开始）。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 请求开始时间起点（字符串，本地时间语义）。
    /// </summary>
    public string? StartedAtStart { get; init; }

    /// <summary>
    /// 请求开始时间终点（字符串，本地时间语义）。
    /// </summary>
    public string? StartedAtEnd { get; init; }

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
