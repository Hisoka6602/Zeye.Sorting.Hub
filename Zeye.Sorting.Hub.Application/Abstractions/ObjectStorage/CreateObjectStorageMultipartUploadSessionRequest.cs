namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 创建 Multipart 上传会话请求。
/// </summary>
public readonly record struct CreateObjectStorageMultipartUploadSessionRequest {
    /// <summary>
    /// 目标 Bucket 名称。
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// 目标对象键。
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// 内容类型。
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// 对象总大小（字节）。
    /// </summary>
    public long? ObjectSizeBytes { get; init; }

    /// <summary>
    /// 建议分片大小（字节）。
    /// </summary>
    public int? PartSizeBytes { get; init; }
}
