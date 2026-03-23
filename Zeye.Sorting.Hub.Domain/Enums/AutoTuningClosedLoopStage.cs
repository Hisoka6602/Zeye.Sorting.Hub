using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 自动调优闭环自治阶段模型，定义从观测到回退的完整执行链路。
    /// </summary>
    public enum AutoTuningClosedLoopStage {
        /// <summary>
        /// 监测阶段：持续观测运行指标并识别可调优信号。
        /// </summary>
        [Description("监测")]
        Monitor,
        /// <summary>
        /// 诊断阶段：分析异常或退化原因并生成调优判断依据。
        /// </summary>
        [Description("诊断")]
        Diagnose,
        /// <summary>
        /// 执行阶段：应用调优动作并推动策略生效。
        /// </summary>
        [Description("执行")]
        Execute,
        /// <summary>
        /// 校验阶段：核验调优结果是否达到预期目标。
        /// </summary>
        [Description("校验")]
        Verify,
        /// <summary>
        /// 回滚阶段：调优结果不达标时恢复到安全基线。
        /// </summary>
        [Description("回滚")]
        Rollback
    }
}
