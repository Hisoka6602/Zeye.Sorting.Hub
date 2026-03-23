using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Sharding {

    /// <summary>
    /// Parcel 主分表策略模式。
    /// </summary>
    public enum ParcelShardingStrategyMode {
        /// <summary>
        /// 仅按时间策略执行分表。
        /// </summary>
        [Description("仅时间策略")]
        Time = 0,

        /// <summary>
        /// 仅按容量阈值策略治理分表。
        /// </summary>
        [Description("仅容量策略")]
        Volume = 1,

        /// <summary>
        /// 组合时间与容量策略进行分表治理。
        /// </summary>
        [Description("时间+容量混合策略")]
        Hybrid = 2
    }
}
