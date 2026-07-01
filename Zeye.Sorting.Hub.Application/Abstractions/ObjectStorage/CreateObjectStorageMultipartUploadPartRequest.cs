namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 创建 Multipart 分片上传预签名会话请求。
/// </summary>
public readonly record struct CreateObjectStorageMultipartUploadPartRequest {
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
    /// 分片序号。
    /// </summary>
    public required int PartNumber { get; init; }
}
