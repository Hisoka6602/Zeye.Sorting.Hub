namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 外部接口请求记录响应合同。
/// </summary>
public sealed record ApiRequestInfoResponse {
    /// <summary>
    /// 接口类型（枚举数值）；无法确定具体业务操作时为空。
    /// </summary>
    public required int? ApiType { get; init; }

    /// <summary>
    /// 请求业务状态（枚举数值）；业务结果未知时为空，不能由 HTTP 状态推断。
    /// </summary>
    public required int? RequestStatus { get; init; }

    /// <summary>
    /// 请求地址。
    /// </summary>
    public required string RequestUrl { get; init; }

    /// <summary>
    /// 参数（URL 或 Query 参数）。
    /// </summary>
    public required string QueryParams { get; init; }

    /// <summary>
    /// 协议头。
    /// </summary>
    public required string Headers { get; init; }

    /// <summary>
    /// 请求内容（原始请求体）。
    /// </summary>
    public required string RequestBody { get; init; }

    /// <summary>
    /// 响应内容（原始响应体）。
    /// </summary>
    public required string ResponseBody { get; init; }

    /// <summary>
    /// 请求时间；来源未上报时为空。
    /// </summary>
    public required DateTime? RequestTime { get; init; }

    /// <summary>
    /// 响应时间。
    /// </summary>
    public required DateTime? ResponseTime { get; init; }

    /// <summary>
    /// 耗时（毫秒）；来源未上报时为空。
    /// </summary>
    public required int? ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 异常信息。
    /// </summary>
    public required string Exception { get; init; }

    /// <summary>
    /// 直接可访问的原始数据。
    /// </summary>
    public required string RawData { get; init; }

    /// <summary>
    /// 格式化后的业务消息。
    /// </summary>
    public required string FormattedMessage { get; init; }

    /// <summary>来源处理记录标识。</summary>
    public string? RecordId { get; init; }

    /// <summary>外部业务 Provider 标识。</summary>
    public string? Provider { get; init; }

    /// <summary>来源处理阶段（枚举数值）。</summary>
    public int? Stage { get; init; }

    /// <summary>来源操作的尝试次数。</summary>
    public int? AttemptNumber { get; init; }

    /// <summary>外部接口响应状态码，独立于业务结果。</summary>
    public int? ResponseStatusCode { get; init; }

    /// <summary>来源处理事实发生时间，不代替未知请求时间。</summary>
    public DateTime? OccurredAt { get; init; }
}
