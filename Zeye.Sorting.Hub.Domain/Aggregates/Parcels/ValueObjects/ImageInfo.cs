using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 图片信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class ImageInfo {
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
    }
}
