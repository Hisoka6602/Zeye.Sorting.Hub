using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// ApiRequestStatus 枚举。
    /// </summary>
    public enum ApiRequestStatus {

        /// <summary>
        /// 未访问
        /// </summary>
        [Description("未访问")]
        NotRequested = 0,

        /// <summary>
        /// 成功
        /// </summary>
        [Description("成功")]
        Success = 1,

        /// <summary>
        /// 失败
        /// </summary>
        [Description("失败")]
        Failed = 2
    }
}
