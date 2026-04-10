using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// ImageCaptureType 枚举。
    /// </summary>
    public enum ImageCaptureType {

        /// <summary>
        /// 相机获取
        /// </summary>
        [Description("相机获取")]
        Camera = 0,

        /// <summary>
        /// 本地匹配
        /// </summary>
        [Description("本地匹配")]
        LocalMatched = 1
    }
}
