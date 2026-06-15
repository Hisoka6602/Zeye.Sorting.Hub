namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// Multipart 上传会话。
/// </summary>
public readonly record struct ObjectStorageMultipartUploadSession {
    /// <summary>
    /// 目标 Bucket 名称。
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// 目标对象键。
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// Multipart UploadId。
    /// </summary>
    public required string UploadId { get; init; }

    /// <summary>
    /// 建议分片大小（字节）。
    /// </summary>
    public required int PartSizeBytes { get; init; }

    /// <summary>
    /// 预估分片数量。
    /// </summary>
    public int? EstimatedPartCount { get; init; }

    /// <summary>
    /// 会话到期时间（本地时间）。
    /// </summary>
    public required DateTime ExpiresAtLocal { get; init; }
}
