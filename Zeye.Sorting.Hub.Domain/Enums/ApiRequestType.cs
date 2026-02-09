using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 接口请求类型枚举
    /// </summary>
    public enum ApiRequestType {

        /// <summary>
        /// 请求 Chute（获取可用 Chute 编号）
        /// </summary>
        [Description("Request Chute")]
        RequestChute = 0,

        /// <summary>
        /// 锁 Chute（锁定指定 Chute）
        /// </summary>
        [Description("Lock Chute")]
        LockChute = 1,

        /// <summary>
        /// 解锁（释放已锁定 Chute）
        /// </summary>
        [Description("Unlock Chute")]
        UnlockChute = 2,

        /// <summary>
        /// 上报 Parcel Chute 结果（分拣完成后的状态报告）
        /// </summary>
        [Description("Chute 报告")]
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
        /// 上报集包完成状态（某 Chute 已满或定时集包完成）
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
        /// 齐 Chute
        /// </summary>
        [Description("Align Chute")]
        AlignChute = 9,

        /// <summary>
        /// Chute 切换（切换目标 Chute 编号）
        /// </summary>
        [Description("Chute 切换")]
        SwitchChute = 10,

        /// <summary>
        /// 稽核（触发稽核流程，如称重复核等）
        /// </summary>
        [Description("稽核")]
        Audit = 11
    }
}
