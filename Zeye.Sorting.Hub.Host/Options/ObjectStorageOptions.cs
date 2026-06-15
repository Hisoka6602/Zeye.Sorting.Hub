using Zeye.Sorting.Hub.Domain.Enums.ObjectStorage;

namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// 对象存储总配置。
/// </summary>
public sealed class ObjectStorageOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// 当前对象存储提供器。
    /// 可填写范围：Minio。
    /// </summary>
    public ObjectStorageProvider Provider { get; set; } = ObjectStorageProvider.Minio;

    /// <summary>
    /// MinIO 配置。
    /// </summary>
    public MinioOptions Minio { get; set; } = new();
}
