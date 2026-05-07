namespace Zeye.Sorting.Hub.Contracts.Models.Common;

/// <summary>
/// 运营边界响应合同。
/// </summary>
public sealed record OperationalScopeResponse {
    /// <summary>
    /// 站点编码。
    /// </summary>
    public required string SiteCode { get; init; }

    /// <summary>
    /// 产线编码。
    /// </summary>
    public string? LineCode { get; init; }

    /// <summary>
    /// 设备编码。
    /// </summary>
    public string? DeviceCode { get; init; }

    /// <summary>
    /// 工作站名称。
    /// </summary>
    public required string WorkstationName { get; init; }
}
