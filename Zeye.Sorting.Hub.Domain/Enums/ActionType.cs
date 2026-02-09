using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Domain.Enums {

    public enum ActionType {

        /// <summary>
        /// 无操作
        /// </summary>
        [Description("无")]
        None = 0,

        /// <summary>
        /// 创建包裹
        /// </summary>
        [Description("创建包裹")]
        CreateParcel = 1,

        //发送目标格口信息

        /// <summary>
        /// 落格结果通知
        /// </summary>
        [Description("落格通知")]
        DischargeConfirmation = 3,

        /// <summary>
        /// 心跳信号
        /// </summary>
        [Description("心跳")]
        Heartbeat = 4,

        /// <summary>
        /// 包裹异常信息通知
        /// </summary>
        [Description("包裹异常")]
        ParcelAbnormal = 5,

        //绑定小车

        /// <summary>
        /// 包裹左移
        /// </summary>
        [Description("包裹左移")]
        MoveLeft = 12,

        /// <summary>
        /// 包裹右移
        /// </summary>
        [Description("包裹右移")]
        MoveRight = 13,

        /// <summary>
        /// 包裹居中
        /// </summary>
        [Description("包裹居中")]
        MoveCenter = 14,

        /// <summary>
        /// 锁格操作
        /// </summary>
        [Description("锁格")]
        LockChute = 15,

        /// <summary>
        /// 解锁操作
        /// </summary>
        [Description("解锁")]
        UnlockChute = 16,
    }
}
