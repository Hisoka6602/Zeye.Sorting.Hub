namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 创建单对象上传预签名会话请求。
/// </summary>
public readonly record struct CreateObjectStorageUploadSessionRequest {
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
    /// 原始文件名。
    /// </summary>
    public string? OriginalFileName { get; init; }

    /// <summary>
    /// 对象大小（字节）。
    /// </summary>
    public long? ObjectSizeBytes { get; init; }
}
