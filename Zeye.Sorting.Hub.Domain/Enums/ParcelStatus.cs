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
        /// 落格不匹配
        /// </summary>
        [Description("落格不匹配")]
        ChuteMismatch = 5,

        /// <summary>
        /// 速度不匹配
        /// </summary>
        [Description("速度不匹配")]
        SpeedMismatch = 6,

        /// <summary>
        /// 叠包
        /// </summary>
        [Description("叠包")]
        Overlapping = 7,

        /// <summary>
        /// 锁格
        /// </summary>
        [Description("锁格")]
        LockedChute = 8,

        /// <summary>
        /// 落格失败
        /// </summary>
        [Description("落格失败")]
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
