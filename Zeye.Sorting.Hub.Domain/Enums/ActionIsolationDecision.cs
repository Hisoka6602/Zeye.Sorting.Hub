using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 危险动作隔离决策。
    /// </summary>
    public enum ActionIsolationDecision {
        /// <summary>
        /// 允许执行目标动作。
        /// </summary>
        [Description("执行")]
        Execute,

        /// <summary>
        /// 触发隔离守卫并阻断执行。
        /// </summary>
        [Description("被守卫阻断")]
        BlockedByGuard,

        /// <summary>
        /// 仅执行演练流程，不落地执行动作。
        /// </summary>
        [Description("仅 DryRun")]
        DryRunOnly
    }
}
