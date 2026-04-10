using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// VideoNodeType 枚举。
    /// </summary>
    public enum VideoNodeType {

        /// <summary>
        /// 扫码节点
        /// </summary>
        [Description("扫码")]
        Scan = 0,

        /// <summary>
        /// 落格节点
        /// </summary>
        [Description("落格")]
        Discharge = 1
    }
}
