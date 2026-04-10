using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// ParcelType 枚举。
    /// </summary>
    public enum ParcelType {

        /// <summary>
        /// 普通包裹
        /// </summary>
        [Description("普通包裹")]
        Normal = 0,

        /// <summary>
        /// 大型包裹
        /// </summary>
        [Description("大型包裹")]
        Large = 1,

        /// <summary>
        /// 聚合包裹（多件组合）
        /// </summary>
        [Description("聚合包裹")]
        Aggregated = 2,

        /// <summary>
        /// 超薄包裹
        /// </summary>
        [Description("超薄包裹")]
        UltraThin = 3,

        /// <summary>
        /// 异形件
        /// </summary>
        [Description("异形件")]
        Irregular = 4,

        /// <summary>
        /// 流体包裹
        /// </summary>
        [Description("流体包裹")]
        Liquid = 5,

        /// <summary>
        /// 易碎品
        /// </summary>
        [Description("易碎品")]
        Fragile = 6,
    }
}
