namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 预签名会话。
/// </summary>
public readonly record struct ObjectStoragePresignedSession {
    /// <summary>
    /// 目标 Bucket 名称。
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// 目标对象键。
    /// </summary>
    public required string ObjectKey { get; init; }

    /// <summary>
    /// 预签名地址。
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// HTTP 方法。
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// 预签名到期时间（本地时间）。
    /// </summary>
    public required DateTime ExpiresAtLocal { get; init; }

    /// <summary>
    /// 调用方必须透传的请求头。
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
