using System.ComponentModel;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums {

    /// <summary>
    /// finer-granularity 扩展规划生命周期。
    /// </summary>
    public enum ParcelFinerGranularityPlanLifecycle {
        /// <summary>
        /// 仅输出规划，不执行物理动作。
        /// </summary>
        [Description("仅规划")]
        PlanOnly = 0,

        /// <summary>
        /// 仅输出告警，不生成执行计划。
        /// </summary>
        [Description("仅告警")]
        AlertOnly = 1,

        /// <summary>
        /// 未来可进入受控执行（仍需隔离器与回滚边界）。
        /// </summary>
        [Description("未来可执行")]
        FutureExecutable = 2
    }
}
