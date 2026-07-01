namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 对象存储对象描述。
/// </summary>
public readonly record struct ObjectStorageObjectDescriptor {
    /// <summary>
    /// 目标 Bucket 名称。
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// 目标对象键。
    /// </summary>
    public required string ObjectKey { get; init; }
}
