namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 视频信息响应合同。
/// </summary>
public sealed record VideoInfoResponse {
    /// <summary>
    /// 通道号。
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// NVR 序列号。
    /// </summary>
    public required string NvrSerialNumber { get; init; }

    /// <summary>
    /// 节点类型（枚举数值）。
    /// </summary>
    public required int NodeType { get; init; }
}
