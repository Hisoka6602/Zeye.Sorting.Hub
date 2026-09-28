using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums.Parcels;

/// <summary>包裹融合数据的持久化处理阶段。</summary>
public enum ParcelProcessingStage {
    /// <summary>包裹检测。</summary>
    [Description("包裹检测")]
    Detected = 0,

    /// <summary>DWS报文接收。</summary>
    [Description("DWS报文接收")]
    DwsReceived = 1,

    /// <summary>DWS融合绑定。</summary>
    [Description("DWS融合绑定")]
    DwsBound = 2,

    /// <summary>扫描上传。</summary>
    [Description("扫描上传")]
    ScanUploaded = 3,

    /// <summary>格口决策。</summary>
    [Description("格口决策")]
    ChuteAssigned = 4,

    /// <summary>分拣指令下发。</summary>
    [Description("分拣指令下发")]
    SorterDispatched = 5,

    /// <summary>实际落格。</summary>
    [Description("实际落格")]
    SortingCompleted = 6,

    /// <summary>设备包裹异常。</summary>
    [Description("设备包裹异常")]
    ParcelException = 7,

    /// <summary>落格回传。</summary>
    [Description("落格回传")]
    LandingReported = 8,

    /// <summary>图片登记。</summary>
    [Description("图片登记")]
    ImageRegistered = 9,

    /// <summary>图片上传。</summary>
    [Description("图片上传")]
    ImageUploaded = 10
}

