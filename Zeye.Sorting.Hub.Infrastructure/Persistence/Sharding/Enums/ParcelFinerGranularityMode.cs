using System.ComponentModel;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums {

    /// <summary>
    /// PerDay 仍过热时建议的下一层细粒度模式。
    /// </summary>
    public enum ParcelFinerGranularityMode {
        /// <summary>
        /// 不建议继续细分。
        /// </summary>
        [Description("不继续细分")]
        None = 0,

        /// <summary>
        /// 继续细分到按小时分表（设计与治理层预留）。
        /// </summary>
        [Description("按小时细分")]
        PerHour = 1,

        /// <summary>
        /// 保持按天语义并在日内做 bucket 细分（设计与治理层预留）。
        /// </summary>
        [Description("按天+桶细分")]
        BucketedPerDay = 2
    }
}
