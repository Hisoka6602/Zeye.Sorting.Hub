using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// AutoTuningClosedLoopStage 枚举。
    /// </summary>
    public enum AutoTuningClosedLoopStage {
        /// <summary>
        /// 监测阶段。
        /// </summary>
        [Description("监测")]
        Monitor,
        /// <summary>
        /// 诊断阶段。
        /// </summary>
        [Description("诊断")]
        Diagnose,
        /// <summary>
        /// 执行阶段。
        /// </summary>
        [Description("执行")]
        Execute,
        /// <summary>
        /// 校验阶段。
        /// </summary>
        [Description("校验")]
        Verify,
        /// <summary>
        /// 回滚阶段。
        /// </summary>
        [Description("回滚")]
        Rollback
    }
}
