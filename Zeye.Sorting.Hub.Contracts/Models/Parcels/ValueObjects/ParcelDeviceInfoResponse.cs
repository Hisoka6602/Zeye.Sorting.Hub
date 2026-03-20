namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 包裹所属设备信息响应合同。
/// </summary>
public sealed record ParcelDeviceInfoResponse {
    /// <summary>
    /// 工作台名称。
    /// </summary>
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 设备机器码。
    /// </summary>
    public required string MachineCode { get; init; }

    /// <summary>
    /// 设备自定义名称。
    /// </summary>
    public required string CustomName { get; init; }
}
