using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 条码信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射信息
    /// </summary>
    public sealed record class BarCodeInfo {
        /// <summary>
        /// 条码
        /// </summary>
        [MaxLength(128)]
        public required string BarCode { get; init; }

        /// <summary>
        /// 条码类型
        /// </summary>
        public required BarCodeType BarCodeType { get; init; }

        /// <summary>
        /// 采集时间（可选）
        /// </summary>
        public DateTime? CapturedTime { get; init; }
    }
}
