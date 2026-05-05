using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// Parcel 新增请求映射器。
/// </summary>
public static class ParcelCreateRequestMapper {
    /// <summary>
    /// 将新增请求映射为 Parcel 聚合根。
    /// </summary>
    /// <param name="request">新增请求合同。</param>
    /// <param name="scannedTime">已解析的扫码时间。</param>
    /// <param name="dischargeTime">已解析的落格时间。</param>
    /// <returns>Parcel 聚合根。</returns>
    public static Parcel MapToParcel(ParcelCreateRequest request, DateTime scannedTime, DateTime dischargeTime) {
        ArgumentNullException.ThrowIfNull(request);

        EnumGuard.ThrowIfUndefined<ParcelType>(request.Type, nameof(request.Type), "包裹类型无效。", "新增包裹");
        EnumGuard.ThrowIfUndefined<ApiRequestStatus>(request.RequestStatus, nameof(request.RequestStatus), "接口访问状态无效。", "新增包裹");
        EnumGuard.ThrowIfUndefined<NoReadType>(request.NoReadType, nameof(request.NoReadType), "NoRead 类型无效。", "新增包裹");

        return Parcel.Create(
            id: request.Id,
            parcelTimestamp: request.ParcelTimestamp,
            type: (ParcelType)request.Type,
            barCodes: request.BarCodes,
            weight: request.Weight,
            workstationName: request.WorkstationName,
            scannedTime: scannedTime,
            dischargeTime: dischargeTime,
            targetChuteId: request.TargetChuteId,
            actualChuteId: request.ActualChuteId,
            requestStatus: (ApiRequestStatus)request.RequestStatus,
            bagCode: request.BagCode,
            isSticking: request.IsSticking,
            length: request.Length,
            width: request.Width,
            height: request.Height,
            volume: request.Volume,
            hasImages: request.HasImages,
            hasVideos: request.HasVideos,
            coordinate: request.Coordinate,
            noReadType: (NoReadType)request.NoReadType,
            sorterCarrierId: request.SorterCarrierId,
            segmentCodes: request.SegmentCodes,
            lifecycleMilliseconds: request.LifecycleMilliseconds);
    }
}
