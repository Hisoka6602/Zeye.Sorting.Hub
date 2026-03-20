namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 包裹平面坐标信息响应合同。
/// </summary>
public sealed record ParcelPositionInfoResponse {
    /// <summary>
    /// 最小 X 坐标（左侧边界）。
    /// </summary>
    public required decimal X1 { get; init; }

    /// <summary>
    /// 最大 X 坐标（右侧边界）。
    /// </summary>
    public required decimal X2 { get; init; }

    /// <summary>
    /// 最小 Y 坐标（上边界）。
    /// </summary>
    public required decimal Y1 { get; init; }

    /// <summary>
    /// 最大 Y 坐标（下边界）。
    /// </summary>
    public required decimal Y2 { get; init; }

    /// <summary>
    /// 背景区域最小 X 坐标（左侧边界）。
    /// </summary>
    public required decimal BackgroundX1 { get; init; }

    /// <summary>
    /// 背景区域最大 X 坐标（右侧边界）。
    /// </summary>
    public required decimal BackgroundX2 { get; init; }

    /// <summary>
    /// 背景区域最小 Y 坐标（上边界）。
    /// </summary>
    public required decimal BackgroundY1 { get; init; }

    /// <summary>
    /// 背景区域最大 Y 坐标（下边界）。
    /// </summary>
    public required decimal BackgroundY2 { get; init; }
}
