namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// Multipart 分片完成标记。
/// </summary>
public readonly record struct ObjectStorageMultipartPartETag {
    /// <summary>
    /// 分片序号。
    /// </summary>
    public required int PartNumber { get; init; }

    /// <summary>
    /// 分片 ETag。
    /// </summary>
    public required string ETag { get; init; }
}
