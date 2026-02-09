using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// Parcel 状态。
    /// </summary>
    public enum ParcelStatus {

        /// <summary>
        /// 待操作
        /// </summary>
        [Description("待操作")]
        Pending = 0,

        /// <summary>
        /// 已完成
        /// </summary>
        [Description("已完成")]
        Completed = 1,

        /// <summary>
        /// 接口异常
        /// </summary>
        [Description("接口异常")]
        InterfaceError = 2,

        /// <summary>
        /// DWS 数据获取超时
        /// </summary>
        [Description("DWS数据获取超时")]
        DwsTimeout = 3,

        /// <summary>
        /// Chute 不匹配
        /// </summary>
        [Description("Chute 不匹配")]
        ChuteMismatch = 5,

        /// <summary>
        /// 速度不匹配
        /// </summary>
        [Description("速度不匹配")]
        SpeedMismatch = 6,

        /// <summary>
        /// Multiple parcels
        /// </summary>
        [Description("Multiple parcels")]
        MultipleParcels = 7,

        /// <summary>
        /// 锁 Chute
        /// </summary>
        [Description("Locked Chute")]
        LockedChute = 8,

        /// <summary>
        /// Chute 失败
        /// </summary>
        [Description("Chute 失败")]
        DischargeFailure = 9,

        /// <summary>
        /// 灰度仪返回异常
        /// </summary>
        [Description("灰度仪返回异常")]
        GrayScaleSensorError = 10,

        /// <summary>
        /// 位置异常
        /// </summary>
        [Description("位置异常")]
        PositionError = 11,

        /// <summary>
        /// 空 Parcel 过期
        /// </summary>
        [Description("空包裹过期")]
        EmptyParcelExpired = 12,
    }
}
