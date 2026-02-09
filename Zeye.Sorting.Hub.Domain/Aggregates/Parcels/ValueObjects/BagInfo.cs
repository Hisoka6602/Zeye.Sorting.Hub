using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects {
    /// <summary>
    /// 集包信息（值对象）
    /// 说明：每个格口对应一个 Bag
    /// </summary>
    public sealed record class BagInfo {
        /// <summary>
        /// 格口 Id（对应目标格口编号）
        /// </summary>
        public required long ChuteId { get; init; }

        /// <summary>
        /// 格口名称（例如 A01、B12，可用于界面展示）
        /// </summary>
        public required string ChuteName { get; init; }

        /// <summary>
        /// 集包号（系统生成的唯一标识，可用于轨迹追溯）
        /// </summary>
        public required string BagCode { get; init; }

        /// <summary>
        /// 当前集包中包裹数量
        /// </summary>
        public int ParcelCount { get; init; }

        /// <summary>
        /// 集包完成时间
        /// </summary>
        public DateTime? BaggingTime { get; init; }
    }
}
