namespace Zeye.Sorting.Hub.Contracts.Models.Common;

/// <summary>
/// 运营边界请求合同。
/// </summary>
public sealed record OperationalScopeRequest {
    /// <summary>
    /// 站点编码（必填，长度范围：1~64）。
    /// </summary>
    public required string SiteCode { get; init; }

    /// <summary>
    /// 产线编码（可空；空白会归一化为 null；非空时长度范围：1~64）。
    /// </summary>
    public string? LineCode { get; init; }

    /// <summary>
    /// 设备编码（可空；空白会归一化为 null；非空时长度范围：1~64）。
    /// </summary>
    public string? DeviceCode { get; init; }

    /// <summary>
    /// 工作站名称（必填，长度范围：1~128）。
    /// </summary>
    public required string WorkstationName { get; init; }
}
