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

    /// <summary>
    /// 使用列表项合同与值对象集合构造详情合同，避免调用方逐字段复制扁平字段。
    /// </summary>
    /// <param name="listItem">列表项合同（提供全部扁平字段）。</param>
    /// <param name="barCodeInfos">条码明细集合。</param>
    /// <param name="weightInfos">称重明细集合。</param>
    /// <param name="volumeInfo">体积信息。</param>
    /// <param name="apiRequests">接口请求记录集合。</param>
    /// <param name="chuteInfo">格口信息。</param>
    /// <param name="commandInfos">通信指令记录集合。</param>
    /// <param name="imageInfos">图片信息集合。</param>
    /// <param name="videoInfos">视频信息集合。</param>
    /// <param name="sorterCarrierInfo">小车信息。</param>
    /// <param name="bagInfo">集包信息。</param>
    /// <param name="deviceInfo">设备信息。</param>
    /// <param name="grayDetectorInfo">灰度仪判断信息。</param>
    /// <param name="stickingParcelInfo">除叠仪判断信息。</param>
    /// <param name="parcelPositionInfo">坐标检测信息。</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ParcelDetailResponse(
        ParcelListItemResponse listItem,
        IReadOnlyList<BarCodeInfoResponse> barCodeInfos,
        IReadOnlyList<WeightInfoResponse> weightInfos,
        VolumeInfoResponse? volumeInfo,
        IReadOnlyList<ApiRequestInfoResponse> apiRequests,
        ChuteInfoResponse? chuteInfo,
        IReadOnlyList<CommandInfoResponse> commandInfos,
        IReadOnlyList<ImageInfoResponse> imageInfos,
        IReadOnlyList<VideoInfoResponse> videoInfos,
        SorterCarrierInfoResponse? sorterCarrierInfo,
        BagInfoResponse? bagInfo,
        ParcelDeviceInfoResponse? deviceInfo,
        GrayDetectorInfoResponse? grayDetectorInfo,
        StickingParcelInfoResponse? stickingParcelInfo,
        ParcelPositionInfoResponse? parcelPositionInfo)
        : base(listItem ?? throw new ArgumentNullException(nameof(listItem))) {
        BarCodeInfos = barCodeInfos;
        WeightInfos = weightInfos;
        VolumeInfo = volumeInfo;
        ApiRequests = apiRequests;
        ChuteInfo = chuteInfo;
        CommandInfos = commandInfos;
        ImageInfos = imageInfos;
        VideoInfos = videoInfos;
        SorterCarrierInfo = sorterCarrierInfo;
        BagInfo = bagInfo;
        DeviceInfo = deviceInfo;
        GrayDetectorInfo = grayDetectorInfo;
        StickingParcelInfo = stickingParcelInfo;
        ParcelPositionInfo = parcelPositionInfo;
    }
}
