using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 接口请求类型枚举
    /// </summary>
    public enum ApiRequestType {

        /// <summary>
        /// 请求格口（获取可用格口编号）
        /// </summary>
        [Description("请求格口")]
        RequestChute = 0,

        /// <summary>
        /// 锁格（锁定指定格口）
        /// </summary>
        [Description("锁格")]
        LockChute = 1,

        /// <summary>
        /// 解锁（释放已锁定格口）
        /// </summary>
        [Description("解锁")]
        UnlockChute = 2,

        /// <summary>
        /// 上报包裹落格结果（分拣完成后的状态报告）
        /// </summary>
        [Description("落格报告")]
        DischargeReport = 3,

        /// <summary>
        /// 上传包裹图像、扫描图等文件
        /// </summary>
        [Description("上传图片")]
        UploadImage = 4,

        /// <summary>
        /// 扫码结果上报（包含条码、时间、设备等）
        /// </summary>
        [Description("扫描")]
        ScanResult = 5,

        /// <summary>
        /// 上报集包完成状态（某格口已满或定时集包完成）
        /// </summary>
        [Description("集包报告")]
        GroupingReport = 6,

        /// <summary>
        /// 补充条码（手动补录缺失或错误条码）
        /// </summary>
        [Description("补充条码")]
        SupplementBarcode = 7,

        /// <summary>
        /// 设备信息查询（获取设备状态、版本等信息）
        /// </summary>
        [Description("设备信息查询")]
        QueryDeviceInfo = 8,

        /// <summary>
        /// 齐格
        /// </summary>
        [Description("齐格")]
        AlignChute = 9,

        /// <summary>
        /// 格口切换（切换目标格口编号）
        /// </summary>
        [Description("格口切换")]
        SwitchChute = 10,

        /// <summary>
        /// 稽核（触发稽核流程，如称重复核等）
        /// </summary>
        [Description("稽核")]
        Audit = 11
    }
}
