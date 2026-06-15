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
    /// 对象存储提供器（枚举数值）。
    /// </summary>
    public int? StorageProvider { get; init; }

    /// <summary>
    /// 对象存储 Bucket 名称。
    /// </summary>
    public string? BucketName { get; init; }

    /// <summary>
    /// 对象键。
    /// </summary>
    public string? ObjectKey { get; init; }

    /// <summary>
    /// 内容类型。
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// 对象大小（字节）。
    /// </summary>
    public long? ObjectSizeBytes { get; init; }

    /// <summary>
    /// 对象 ETag。
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// 对象 SHA256 摘要。
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// 上传完成时间（本地时间）。
    /// </summary>
    public DateTime? UploadedAtLocal { get; init; }

    /// <summary>
    /// 原始文件名。
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// 图片获取方式（枚举数值）。
    /// </summary>
    public required int CaptureType { get; init; }
}
