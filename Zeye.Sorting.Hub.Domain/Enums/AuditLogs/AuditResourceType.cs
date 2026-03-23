using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.AuditLogs {

    /// <summary>
    /// 审计资源类型枚举。
    /// </summary>
    public enum AuditResourceType {
        /// <summary>
        /// 未指定资源类型。
        /// </summary>
        [Description("未指定")]
        Unknown = 0,

        /// <summary>
        /// 接口资源。
        /// </summary>
        [Description("接口")]
        Api = 1,

        /// <summary>
        /// 文件资源。
        /// </summary>
        [Description("文件")]
        File = 2,

        /// <summary>
        /// 图片资源。
        /// </summary>
        [Description("图片")]
        Image = 3,

        /// <summary>
        /// 数据库资源。
        /// </summary>
        [Description("数据库")]
        Database = 4,

        /// <summary>
        /// 业务对象资源。
        /// </summary>
        [Description("业务对象")]
        BusinessObject = 5
    }
}
