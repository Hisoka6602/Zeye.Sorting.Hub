using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.ObjectStorage {

    /// <summary>
    /// 对象存储提供器枚举。
    /// </summary>
    public enum ObjectStorageProvider {

        /// <summary>
        /// 本地文件系统。
        /// </summary>
        [Description("本地文件系统")]
        LocalFileSystem = 0,

        /// <summary>
        /// MinIO 对象存储。
        /// </summary>
        [Description("MinIO 对象存储")]
        Minio = 1
    }
}
