using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums {

    /// <summary>
    /// 包裹异常类型。
    /// </summary>
    public enum ParcelExceptionType {

        /// <summary>所有来源异常分类规则均未匹配时使用的系统兜底类型。</summary>
        [Description("未知异常")]
        Unknown = 0,

        /// <summary>
        /// 接口响应异常
        /// </summary>
        [Description("接口响应异常")]
        InterfaceResponseException = 1,

        /// <summary>
        /// 等待 DWS 数据超时
        /// </summary>
        [Description("等待DWS数据超时")]
        WaitDwsDataTimeout = 2,

        /// <summary>
        /// 等待目标格口超时
        /// </summary>
        [Description("等待目标格口超时")]
        WaitTargetChuteTimeout = 3,

        /// <summary>
        /// 无效目标格口
        /// </summary>
        [Description("无效目标格口")]
        InvalidTargetChute = 4,

        /// <summary>
        /// 速度不匹配
        /// </summary>
        [Description("速度不匹配")]
        SpeedMismatch = 5,

        /// <summary>
        /// 锁格
        /// </summary>
        [Description("锁格")]
        LockedChute = 6,

        /// <summary>
        /// 叠包
        /// </summary>
        [Description("叠包")]
        StickingParcel = 7,

        /// <summary>
        /// 灰度仪响应异常
        /// </summary>
        [Description("灰度仪响应异常")]
        GrayDetectorResponseException = 8,

        /// <summary>
        /// 位置检测异常
        /// </summary>
        [Description("位置检测异常")]
        PositionDetectionException = 9,

        /// <summary>
        /// 包裹丢失
        /// </summary>
        [Description("包裹丢失")]
        ParcelLost = 10,

        /// <summary>
        /// 机械故障
        /// </summary>
        [Description("机械故障")]
        MechanicalFailure = 11,

        /// <summary>
        /// 飘格
        /// </summary>
        [Description("飘格")]
        DriftChute = 12,

        /// <summary>设备检测到包裹间距违规。</summary>
        [Description("包裹间距违规")]
        ParcelSpacingViolation = 13,

        /// <summary>目标格口分配被拒绝。</summary>
        [Description("目标格口分配被拒绝")]
        TargetChuteAssignmentRejected = 14,

        /// <summary>其他来源设备异常，具体编码保存在处理事实中。</summary>
        [Description("来源设备异常")]
        SourceDeviceException = 15,
    }
}
