using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 体积信息（值对象）
    /// 说明：仅表达领域语义，decimal 精度通过特征标记声明
    /// </summary>
    public sealed record class VolumeInfo {
        /// <summary>
        /// 体积来源类型（如 3D 相机、人工输入等）
        /// </summary>
        public required VolumeSourceType SourceType { get; init; }

        /// <summary>
        /// 原始体积字符串（设备返回的原始格式）
        /// </summary>
        [MaxLength(512)]
        public string RawVolume { get; init; } = string.Empty;

        /// <summary>
        /// 取证依据（如图像编号、3D 模型编号等）
        /// </summary>
        [MaxLength(128)]
        public string EvidenceCode { get; init; } = string.Empty;

        /// <summary>
        /// 格式化后的长度（单位：毫米）
        /// </summary>
        [Precision(18, 3)]
        public required decimal FormattedLength { get; init; }

        /// <summary>
        /// 格式化后的宽度（单位：毫米）
        /// </summary>
        [Precision(18, 3)]
        public required decimal FormattedWidth { get; init; }

        /// <summary>
        /// 格式化后的高度（单位：毫米）
        /// </summary>
        [Precision(18, 3)]
        public required decimal FormattedHeight { get; init; }

        /// <summary>
        /// 格式化后的体积（单位：立方厘米）
        /// </summary>
        [Precision(18, 3)]
        public required decimal FormattedVolume { get; init; }

        /// <summary>
        /// 长度调整值（单位：毫米；为空表示未调整）
        /// </summary>
        [Precision(18, 3)]
        public decimal? AdjustedLength { get; init; }

        /// <summary>
        /// 宽度调整值（单位：毫米；为空表示未调整）
        /// </summary>
        [Precision(18, 3)]
        public decimal? AdjustedWidth { get; init; }

        /// <summary>
        /// 高度调整值（单位：毫米；为空表示未调整）
        /// </summary>
        [Precision(18, 3)]
        public decimal? AdjustedHeight { get; init; }

        /// <summary>
        /// 体积调整值（单位：立方厘米；为空表示未调整）
        /// </summary>
        [Precision(18, 3)]
        public decimal? AdjustedVolume { get; init; }

        /// <summary>
        /// 测量时间（设备实际采集时间点）
        /// </summary>
        public DateTime MeasurementTime { get; init; }

        /// <summary>
        /// 体积绑定时间
        /// </summary>
        public DateTime? BindTime { get; init; }

        /// <summary>
        /// 获取生效长度（优先使用调整值）
        /// </summary>
        public decimal GetEffectiveLength() => AdjustedLength ?? FormattedLength;

        /// <summary>
        /// 获取生效宽度（优先使用调整值）
        /// </summary>
        public decimal GetEffectiveWidth() => AdjustedWidth ?? FormattedWidth;

        /// <summary>
        /// 获取生效高度（优先使用调整值）
        /// </summary>
        public decimal GetEffectiveHeight() => AdjustedHeight ?? FormattedHeight;

        /// <summary>
        /// 获取生效体积（优先使用调整值）
        /// </summary>
        public decimal GetEffectiveVolume() => AdjustedVolume ?? FormattedVolume;
    }
}
