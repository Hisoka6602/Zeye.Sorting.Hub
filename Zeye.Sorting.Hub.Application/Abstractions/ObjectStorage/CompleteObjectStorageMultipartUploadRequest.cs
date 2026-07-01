namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 完成 Multipart 上传请求。
/// </summary>
public readonly record struct CompleteObjectStorageMultipartUploadRequest {
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
    /// 已上传分片集合。
    /// </summary>
    public required IReadOnlyList<ObjectStorageMultipartPartETag> Parts { get; init; }
}
