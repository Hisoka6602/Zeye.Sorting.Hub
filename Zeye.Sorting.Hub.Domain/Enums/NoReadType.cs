using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// NoReadType 枚举。
    /// </summary>
    public enum NoReadType {

        /// <summary>
        /// 无类型（默认值）
        /// </summary>
        [Description("无类型")]
        None = 0,

        /// <summary>
        /// 无包裹（摄像头未识别到任何包裹）
        /// </summary>
        [Description("画面无包裹")]
        NoParcel = 1,

        /// <summary>
        /// 无面单（包裹上未检测到面单）
        /// </summary>
        [Description("无面单")]
        NoWaybill = 2,

        /// <summary>
        /// 面单模糊（图像模糊，无法识别条码）
        /// </summary>
        [Description("面单模糊")]
        BlurryWaybill = 3,

        /// <summary>
        /// 面单褶皱（条码因褶皱导致识别失败）
        /// </summary>
        [Description("面单褶皱")]
        WrinkledWaybill = 4,

        /// <summary>
        /// 条码截断（部分条码缺失）
        /// </summary>
        [Description("条码截断")]
        TruncatedBarcode = 5,

        /// <summary>
        /// 反光（条码区域反光，无法识别）
        /// </summary>
        [Description("反光")]
        Reflection = 6,

        /// <summary>
        /// 光线不足（图像过暗，无法识别）
        /// </summary>
        [Description("光线不足")]
        InsufficientLighting = 7,

        /// <summary>
        /// 条码污损（条码被遮挡/污染导致无法识别）
        /// </summary>
        [Description("条码污损")]
        DirtyBarcode = 8,

        /// <summary>
        /// 对焦模糊（相机对焦失败导致图像无法识别）
        /// </summary>
        [Description("对焦模糊")]
        OutOfFocus = 9,
    }
}
