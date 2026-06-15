using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.ObjectStorage;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 图片信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class ImageInfo {
        /// <summary>
        /// 关联包裹主键（仅用于基础设施层持久化映射与分片路由，不参与领域业务输入）
        /// </summary>
        public long ParcelId { get; private init; }

        /// <summary>
        /// 相机名称（设备内置名称或型号）
        /// </summary>
        [MaxLength(128)]
        public string CameraName { get; init; } = string.Empty;

        /// <summary>
        /// 相机自定义名（如 TopCam、SideCam、VolumeCam）
        /// </summary>
        [MaxLength(128)]
        public string CustomName { get; init; } = string.Empty;

        /// <summary>
        /// 相机序列号（设备唯一标识）
        /// </summary>
        [MaxLength(128)]
        public string CameraSerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// 图片类型（扫码、全景、体积云点、体积彩图、面单抠图等）
        /// </summary>
        public required ImageType ImageType { get; init; }

        /// <summary>
        /// 图片相对路径（相对于存储根目录）
        /// </summary>
        [MaxLength(1024)]
        public required string RelativePath { get; init; }

        /// <summary>
        /// 对象存储提供器。
        /// </summary>
        public ObjectStorageProvider? StorageProvider { get; init; }

        /// <summary>
        /// 对象存储 Bucket 名称。
        /// </summary>
        [MaxLength(128)]
        public string? BucketName { get; init; }

        /// <summary>
        /// 对象键。
        /// </summary>
        [MaxLength(1024)]
        public string? ObjectKey { get; init; }

        /// <summary>
        /// 内容类型。
        /// </summary>
        [MaxLength(256)]
        public string? ContentType { get; init; }

        /// <summary>
        /// 对象大小（字节）。
        /// </summary>
        public long? ObjectSizeBytes { get; init; }

        /// <summary>
        /// 对象 ETag。
        /// </summary>
        [MaxLength(128)]
        public string? ETag { get; init; }

        /// <summary>
        /// 对象 SHA256 摘要。
        /// </summary>
        [MaxLength(128)]
        public string? Sha256 { get; init; }

        /// <summary>
        /// 上传完成时间（本地时间）。
        /// </summary>
        public DateTime? UploadedAtLocal { get; init; }

        /// <summary>
        /// 原始文件名。
        /// </summary>
        [MaxLength(256)]
        public string? OriginalFileName { get; init; }

        /// <summary>
        /// 图片获取方式（相机获取、本地匹配等）
        /// </summary>
        public required ImageCaptureType CaptureType { get; init; }

        /// <summary>
        /// 按领域语义比较图片信息（忽略仅用于基础设施映射的 ParcelId）。
        /// </summary>
        /// <param name="other">待比较对象。</param>
        /// <returns>当领域字段一致时返回 true。</returns>
        public bool Equals(ImageInfo? other) {
            return other is not null
                && string.Equals(CameraName, other.CameraName, StringComparison.Ordinal)
                && string.Equals(CustomName, other.CustomName, StringComparison.Ordinal)
                && string.Equals(CameraSerialNumber, other.CameraSerialNumber, StringComparison.Ordinal)
                && ImageType == other.ImageType
                && string.Equals(RelativePath, other.RelativePath, StringComparison.Ordinal)
                && StorageProvider == other.StorageProvider
                && string.Equals(BucketName, other.BucketName, StringComparison.Ordinal)
                && string.Equals(ObjectKey, other.ObjectKey, StringComparison.Ordinal)
                && string.Equals(ContentType, other.ContentType, StringComparison.Ordinal)
                && ObjectSizeBytes == other.ObjectSizeBytes
                && string.Equals(ETag, other.ETag, StringComparison.Ordinal)
                && string.Equals(Sha256, other.Sha256, StringComparison.Ordinal)
                && Nullable.Equals(UploadedAtLocal, other.UploadedAtLocal)
                && string.Equals(OriginalFileName, other.OriginalFileName, StringComparison.Ordinal)
                && CaptureType == other.CaptureType;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            var hashCode = new HashCode();
            hashCode.Add(CameraName, StringComparer.Ordinal);
            hashCode.Add(CustomName, StringComparer.Ordinal);
            hashCode.Add(CameraSerialNumber, StringComparer.Ordinal);
            hashCode.Add(ImageType);
            hashCode.Add(RelativePath, StringComparer.Ordinal);
            hashCode.Add(StorageProvider);
            hashCode.Add(BucketName, StringComparer.Ordinal);
            hashCode.Add(ObjectKey, StringComparer.Ordinal);
            hashCode.Add(ContentType, StringComparer.Ordinal);
            hashCode.Add(ObjectSizeBytes);
            hashCode.Add(ETag, StringComparer.Ordinal);
            hashCode.Add(Sha256, StringComparer.Ordinal);
            hashCode.Add(UploadedAtLocal);
            hashCode.Add(OriginalFileName, StringComparer.Ordinal);
            hashCode.Add(CaptureType);
            return hashCode.ToHashCode();
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(ImageInfo)} {{ {nameof(CameraName)} = {CameraName}, {nameof(CustomName)} = {CustomName}, {nameof(CameraSerialNumber)} = {CameraSerialNumber}, {nameof(ImageType)} = {ImageType}, {nameof(RelativePath)} = {RelativePath}, {nameof(StorageProvider)} = {StorageProvider}, {nameof(BucketName)} = {BucketName}, {nameof(ObjectKey)} = {ObjectKey}, {nameof(ContentType)} = {ContentType}, {nameof(ObjectSizeBytes)} = {ObjectSizeBytes}, {nameof(ETag)} = {ETag}, {nameof(Sha256)} = {Sha256}, {nameof(UploadedAtLocal)} = {UploadedAtLocal}, {nameof(OriginalFileName)} = {OriginalFileName}, {nameof(CaptureType)} = {CaptureType} }}";
        }
    }
}
