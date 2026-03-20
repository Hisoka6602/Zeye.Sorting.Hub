using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 详情响应合同（包含所有联表值对象内容）。
/// </summary>
public sealed record ParcelDetailResponse : ParcelListItemResponse {
    /// <summary>
    /// 条码明细集合。
    /// </summary>
    public required IReadOnlyList<BarCodeInfoResponse> BarCodeInfos { get; init; }

    /// <summary>
    /// 称重明细集合。
    /// </summary>
    public required IReadOnlyList<WeightInfoResponse> WeightInfos { get; init; }

    /// <summary>
    /// 体积信息（单值对象）。
    /// </summary>
    public required VolumeInfoResponse? VolumeInfo { get; init; }

    /// <summary>
    /// 外部接口请求记录集合。
    /// </summary>
    public required IReadOnlyList<ApiRequestInfoResponse> ApiRequests { get; init; }

    /// <summary>
    /// 格口信息（单值对象）。
    /// </summary>
    public required ChuteInfoResponse? ChuteInfo { get; init; }

    /// <summary>
    /// 通信指令记录集合。
    /// </summary>
    public required IReadOnlyList<CommandInfoResponse> CommandInfos { get; init; }

    /// <summary>
    /// 图片信息集合。
    /// </summary>
    public required IReadOnlyList<ImageInfoResponse> ImageInfos { get; init; }

    /// <summary>
    /// 视频信息集合。
    /// </summary>
    public required IReadOnlyList<VideoInfoResponse> VideoInfos { get; init; }

    /// <summary>
    /// 小车信息（单值对象）。
    /// </summary>
    public required SorterCarrierInfoResponse? SorterCarrierInfo { get; init; }

    /// <summary>
    /// 集包信息（单值对象）。
    /// </summary>
    public required BagInfoResponse? BagInfo { get; init; }

    /// <summary>
    /// 设备信息（单值对象）。
    /// </summary>
    public required ParcelDeviceInfoResponse? DeviceInfo { get; init; }

    /// <summary>
    /// 灰度仪判断信息（单值对象）。
    /// </summary>
    public required GrayDetectorInfoResponse? GrayDetectorInfo { get; init; }

    /// <summary>
    /// 除叠仪判断信息（单值对象）。
    /// </summary>
    public required StickingParcelInfoResponse? StickingParcelInfo { get; init; }

    /// <summary>
    /// 坐标检测信息（单值对象）。
    /// </summary>
    public required ParcelPositionInfoResponse? ParcelPositionInfo { get; init; }
}
