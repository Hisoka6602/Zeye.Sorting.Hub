using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 数据库迁移失败策略。
    /// </summary>
    public enum MigrationFailureMode {
        /// <summary>
        /// 失败后降级运行。
        /// </summary>
        [Description("降级运行")]
        Degraded = 0,

        /// <summary>
        /// 失败后立即终止启动。
        /// </summary>
        [Description("快速失败")]
        FailFast = 1
    }
}
