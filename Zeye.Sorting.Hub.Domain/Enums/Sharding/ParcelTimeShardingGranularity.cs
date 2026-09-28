using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Sharding {

    /// <summary>
    /// Parcel 时间分表粒度。
    /// </summary>
    public enum ParcelTimeShardingGranularity {
        /// <summary>
        /// 按月分表。
        /// </summary>
        [Description("按月分表")]
        PerMonth = 0,

        /// <summary>
        /// 按天分表。
        /// </summary>
        [Description("按天分表")]
        PerDay = 1,

        /// <summary>按周分表，周期从本地时间周一零点开始。</summary>
        [Description("按周分表")]
        PerWeek = 2
    }
}
