using System.ComponentModel;

namespace Zeye.Sorting.Hub.Host.Enums {

    /// <summary>
    /// 数据库迁移失败策略。
    /// </summary>
    internal enum MigrationFailureMode {
        /// <summary>
        /// 失败后降级运行。
        /// </summary>
        [Description("Degraded")]
        Degraded = 0,

        /// <summary>
        /// 失败后立即终止启动。
        /// </summary>
        [Description("FailFast")]
        FailFast = 1
    }
}
