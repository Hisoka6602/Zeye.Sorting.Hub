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
        /// 创建 Parcel
        /// </summary>
        [Description("创建 Parcel")]
        CreateParcel = 1,

        //发送目标 Chute 信息

        /// <summary>
        /// Chute 结果通知
        /// </summary>
        [Description("Chute 通知")]
        DischargeConfirmation = 3,

        /// <summary>
        /// 心跳信号
        /// </summary>
        [Description("心跳")]
        Heartbeat = 4,

        /// <summary>
        /// Parcel 异常信息通知
        /// </summary>
        [Description("Parcel 异常")]
        ParcelAbnormal = 5,

        //绑定 SorterCarrier

        /// <summary>
        /// Parcel 左移
        /// </summary>
        [Description("Parcel 左移")]
        MoveLeft = 12,

        /// <summary>
        /// Parcel 右移
        /// </summary>
        [Description("Parcel 右移")]
        MoveRight = 13,

        /// <summary>
        /// Parcel 居中
        /// </summary>
        [Description("Parcel 居中")]
        MoveCenter = 14,

        /// <summary>
        /// 锁 Chute 操作
        /// </summary>
        [Description("锁 Chute")]
        LockChute = 15,

        /// <summary>
        /// 解锁 Chute 操作
        /// </summary>
        [Description("解锁 Chute")]
        UnlockChute = 16,
    }
}
