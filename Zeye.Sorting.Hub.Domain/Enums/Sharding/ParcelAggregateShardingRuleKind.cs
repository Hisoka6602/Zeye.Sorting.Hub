using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Sharding {

    /// <summary>
    /// Parcel 聚合分表规则类型枚举。
    /// </summary>
    public enum ParcelAggregateShardingRuleKind {
        /// <summary>
        /// 按日期分表规则。
        /// </summary>
        [Description("按日期分表")]
        Date = 0,

        /// <summary>
        /// 按哈希分表规则。
        /// </summary>
        [Description("按哈希分表")]
        Hash = 1
    }
}
