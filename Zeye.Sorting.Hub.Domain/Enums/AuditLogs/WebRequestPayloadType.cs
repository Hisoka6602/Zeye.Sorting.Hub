using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.AuditLogs {

    /// <summary>
    /// Web 请求载荷类型枚举。
    /// </summary>
    public enum WebRequestPayloadType {
        /// <summary>
        /// 未知请求载荷类型。
        /// </summary>
        [Description("未知")]
        Unknown = 0,

        /// <summary>
        /// 无请求体。
        /// </summary>
        [Description("无请求体")]
        None = 1,

        /// <summary>
        /// JSON 请求体。
        /// </summary>
        [Description("JSON")]
        Json = 2,

        /// <summary>
        /// 表单请求体。
        /// </summary>
        [Description("表单")]
        Form = 3,

        /// <summary>
        /// 多段表单请求体。
        /// </summary>
        [Description("多段表单")]
        MultipartFormData = 4,

        /// <summary>
        /// 纯文本请求体。
        /// </summary>
        [Description("文本")]
        Text = 5,

        /// <summary>
        /// 二进制请求体。
        /// </summary>
        [Description("二进制")]
        Binary = 6
    }
}
