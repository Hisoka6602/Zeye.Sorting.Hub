using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// SorterCarrier 信息（值对象）
    /// 说明：仅表达领域语义，不包含 ORM 映射与序列化特性
    /// </summary>
    public sealed record class SorterCarrierInfo {
        /// <summary>
        /// SorterCarrier 编号（唯一标识 SorterCarrier）
        /// </summary>
        public required int SorterCarrierId { get; init; }

        /// <summary>
        /// Parcel 上 SorterCarrier 时间
        /// </summary>
        public required DateTime LoadedTime { get; init; }

        /// <summary>
        /// 上 SorterCarrier 时输送带速度（单位：mm/s）
        /// </summary>
        public required decimal ConveyorSpeedWhenLoaded { get; init; }

        /// <summary>
        /// 联动 SorterCarrier 数量（默认 1）
        /// </summary>
        public required int LinkedCarrierCount { get; init; } = 1;
    }
}
