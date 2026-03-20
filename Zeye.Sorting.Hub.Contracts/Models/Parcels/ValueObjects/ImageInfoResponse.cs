namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 图片信息响应合同。
/// </summary>
public sealed record ImageInfoResponse {
    /// <summary>
    /// 相机名称。
    /// </summary>
    public required string CameraName { get; init; }

    /// <summary>
    /// 相机自定义名。
    /// </summary>
    public required string CustomName { get; init; }

    /// <summary>
    /// 相机序列号。
    /// </summary>
    public required string CameraSerialNumber { get; init; }

    /// <summary>
    /// 图片类型（枚举数值）。
    /// </summary>
    public required int ImageType { get; init; }

    /// <summary>
    /// 图片相对路径。
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// 图片获取方式（枚举数值）。
    /// </summary>
    public required int CaptureType { get; init; }
}
