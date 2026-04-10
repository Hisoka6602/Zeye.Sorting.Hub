using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

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
                && CaptureType == other.CaptureType;
        }

        /// <summary>
        /// 生成仅基于领域字段的哈希码（忽略 ParcelId）。
        /// </summary>
        /// <returns>领域字段哈希码。</returns>
        public override int GetHashCode() {
            return HashCode.Combine(
                HashCode.Combine(CameraName, CustomName, CameraSerialNumber),
                HashCode.Combine(ImageType, RelativePath, CaptureType));
        }

        /// <summary>
        /// 输出仅包含领域字段的调试字符串（忽略 ParcelId）。
        /// </summary>
        /// <returns>调试字符串。</returns>
        public override string ToString() {
            return $"{nameof(ImageInfo)} {{ {nameof(CameraName)} = {CameraName}, {nameof(CustomName)} = {CustomName}, {nameof(CameraSerialNumber)} = {CameraSerialNumber}, {nameof(ImageType)} = {ImageType}, {nameof(RelativePath)} = {RelativePath}, {nameof(CaptureType)} = {CaptureType} }}";
        }
    }
}
