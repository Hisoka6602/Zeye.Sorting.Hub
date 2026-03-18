using System.ComponentModel;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums {

    /// <summary>
    /// 容量阈值命中后的治理动作。
    /// </summary>
    public enum ParcelVolumeThresholdAction {
        /// <summary>
        /// 命中阈值后仅输出治理告警，不改变当前时间粒度。
        /// </summary>
        [Description("仅告警")]
        AlertOnly = 0,

        /// <summary>
        /// 命中阈值后切换到按天分表决策。
        /// </summary>
        [Description("切换到按天分表")]
        SwitchToPerDay = 1
    }
}
