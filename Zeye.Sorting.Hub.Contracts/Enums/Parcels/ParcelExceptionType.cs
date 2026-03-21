using System.ComponentModel;

namespace Zeye.Sorting.Hub.Contracts.Enums.Parcels;

/// <summary>
/// 包裹异常类型对外合同枚举。
/// 与 Domain 层 ParcelExceptionType 数值保持一一对应，供 API 客户端识别可用值，
/// 避免调用方依赖魔法数字或字符串化状态判断。
/// </summary>
public enum ParcelExceptionType {

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
}
