namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 中止 Multipart 上传请求。
/// </summary>
public readonly record struct AbortObjectStorageMultipartUploadRequest {
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
}
