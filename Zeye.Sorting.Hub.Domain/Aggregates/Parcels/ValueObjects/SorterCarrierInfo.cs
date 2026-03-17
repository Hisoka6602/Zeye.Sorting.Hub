using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 小车信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class SorterCarrierInfo {
        /// <summary>
        /// 小车编号（唯一标识小车）
        /// </summary>
        public required int SorterCarrierId { get; init; }

        /// <summary>
        /// 包裹上车时间
        /// </summary>
        public required DateTime LoadedTime { get; init; }

        /// <summary>
        /// 上车时输送带速度（单位：mm/s）
        /// </summary>
        [Column(TypeName = "decimal(18,3)")]
        public required decimal ConveyorSpeedWhenLoaded { get; init; }

        /// <summary>
        /// 联动小车数量（默认 1）
        /// </summary>
        public required int LinkedCarrierCount { get; init; } = 1;
    }
}
