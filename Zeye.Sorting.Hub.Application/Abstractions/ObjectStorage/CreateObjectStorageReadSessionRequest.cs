namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 创建对象读取预签名会话请求。
/// </summary>
public readonly record struct CreateObjectStorageReadSessionRequest {
    /// <summary>
    /// 目标 Bucket 名称。
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// 目标对象键。
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// 下载文件名。
    /// </summary>
    public string? DownloadFileName { get; init; }
}
