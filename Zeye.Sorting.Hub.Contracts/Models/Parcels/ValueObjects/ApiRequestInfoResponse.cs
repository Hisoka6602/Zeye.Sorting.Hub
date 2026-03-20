namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 外部接口请求记录响应合同（对外仅暴露脱敏后的摘要信息，不暴露原始载荷与鉴权头）。
/// </summary>
public sealed record ApiRequestInfoResponse {
    /// <summary>
    /// 接口类型（枚举数值）。
    /// </summary>
    public required int ApiType { get; init; }

    /// <summary>
    /// 请求状态（枚举数值）。
    /// </summary>
    public required int RequestStatus { get; init; }

    /// <summary>
    /// 请求地址。
    /// </summary>
    public required string RequestUrl { get; init; }

    /// <summary>
    /// 参数（URL 或 Query 参数，不包含敏感字段）。
    /// </summary>
    public required string QueryParams { get; init; }

    /// <summary>
    /// 请求时间（本地时间语义）。
    /// </summary>
    public required DateTime RequestTime { get; init; }

    /// <summary>
    /// 响应时间（本地时间语义）。
    /// </summary>
    public required DateTime? ResponseTime { get; init; }

    /// <summary>
    /// 耗时（毫秒）。
    /// </summary>
    public required int ElapsedMilliseconds { get; init; }

    /// <summary>
    /// 异常信息（已脱敏的错误描述）。
    /// </summary>
    public required string Exception { get; init; }

    /// <summary>
    /// 格式化后的业务消息。
    /// </summary>
    public required string FormattedMessage { get; init; }
}
