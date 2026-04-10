using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// Parcel 状态。
    /// </summary>
    public enum ParcelStatus {

        /// <summary>
        /// 待操作
        /// </summary>
        [Description("待操作")]
        Pending = 0,

        /// <summary>
        /// 已完成
        /// </summary>
        [Description("已完成")]
        Completed = 1,

        /// <summary>
        /// 分拣异常
        /// </summary>
        [Description("分拣异常")]
        SortingException = 2,
    }
}
