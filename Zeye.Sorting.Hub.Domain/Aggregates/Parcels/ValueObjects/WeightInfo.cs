using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 称重信息（值对象）
    /// 说明：仅表达领域语义，decimal 精度通过特征标记声明
    /// </summary>
    public sealed record class WeightInfo {
        /// <summary>
        /// 原始重量字符串（设备传入原始格式）
        /// </summary>
        [MaxLength(512)]
        public string RawWeight { get; init; } = string.Empty;

        /// <summary>
        /// 取证依据（如图像编号、传感器采样编号等）
        /// </summary>
        [MaxLength(128)]
        public string EvidenceCode { get; init; } = string.Empty;

        /// <summary>
        /// 格式化后重量（单位：kg）
        /// </summary>
        [Precision(18, 3)]
        public required decimal FormattedWeight { get; init; }

        /// <summary>
        /// 称重时间（设备实际采集时间点）
        /// </summary>
        public required DateTime WeighingTime { get; init; }

        /// <summary>
        /// 调整后的重量（单位：kg；为空表示未调整）
        /// </summary>
        [Precision(18, 3)]
        public decimal? AdjustedWeight { get; init; }

        /// <summary>
        /// 获取生效重量（优先使用调整值）
        /// </summary>
        public decimal GetEffectiveWeight() => AdjustedWeight ?? FormattedWeight;
    }
}
