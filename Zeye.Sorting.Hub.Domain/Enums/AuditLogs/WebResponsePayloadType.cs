using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.AuditLogs {

    /// <summary>
    /// Web 响应载荷类型枚举。
    /// </summary>
    public enum WebResponsePayloadType {
        /// <summary>
        /// 未知响应载荷类型。
        /// </summary>
        [Description("未知")]
        Unknown = 0,

        /// <summary>
        /// 无响应体。
        /// </summary>
        [Description("无响应体")]
        None = 1,

        /// <summary>
        /// JSON 响应体。
        /// </summary>
        [Description("JSON")]
        Json = 2,

        /// <summary>
        /// 纯文本响应体。
        /// </summary>
        [Description("文本")]
        Text = 3,

        /// <summary>
        /// HTML 响应体。
        /// </summary>
        [Description("HTML")]
        Html = 4,

        /// <summary>
        /// 二进制响应体。
        /// </summary>
        [Description("二进制")]
        Binary = 5
    }
}
