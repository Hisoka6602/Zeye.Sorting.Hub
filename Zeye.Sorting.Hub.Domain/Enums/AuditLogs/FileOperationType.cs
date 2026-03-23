using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.AuditLogs {

    /// <summary>
    /// 文件操作类型枚举。
    /// </summary>
    public enum FileOperationType {
        /// <summary>
        /// 未发生文件操作。
        /// </summary>
        [Description("无")]
        None = 0,

        /// <summary>
        /// 读取文件。
        /// </summary>
        [Description("读取")]
        Read = 1,

        /// <summary>
        /// 写入文件。
        /// </summary>
        [Description("写入")]
        Write = 2,

        /// <summary>
        /// 上传文件。
        /// </summary>
        [Description("上传")]
        Upload = 3,

        /// <summary>
        /// 下载文件。
        /// </summary>
        [Description("下载")]
        Download = 4,

        /// <summary>
        /// 删除文件。
        /// </summary>
        [Description("删除")]
        Delete = 5
    }
}
