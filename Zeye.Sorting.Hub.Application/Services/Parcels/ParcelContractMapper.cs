using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// Parcel 合同映射器。
/// </summary>
internal static class ParcelContractMapper {
    /// <summary>
    /// 将摘要读模型映射为列表项合同。
    /// </summary>
    /// <param name="readModel">仓储摘要读模型。</param>
    /// <returns>列表项合同。</returns>
    public static ParcelListItemResponse ToListItem(ParcelSummaryReadModel readModel) {
        return new ParcelListItemResponse {
            Id = readModel.Id,
            CreatedTime = readModel.CreatedTime,
            ModifyTime = readModel.ModifyTime,
            ModifyIp = readModel.ModifyIp,
            ParcelTimestamp = readModel.ParcelTimestamp,
            Type = (int)readModel.Type,
            Status = (int)readModel.Status,
            ExceptionType = readModel.ExceptionType is null ? null : (int)readModel.ExceptionType.Value,
            NoReadType = (int)readModel.NoReadType,
            SorterCarrierId = readModel.SorterCarrierId,
            SegmentCodes = readModel.SegmentCodes,
            LifecycleMilliseconds = readModel.LifecycleMilliseconds,
            TargetChuteId = readModel.TargetChuteId,
            ActualChuteId = readModel.ActualChuteId,
            BarCodes = readModel.BarCodes,
            Weight = readModel.Weight,
            RequestStatus = (int)readModel.RequestStatus,
            BagCode = readModel.BagCode,
            WorkstationName = readModel.WorkstationName,
            IsSticking = readModel.IsSticking,
            Length = readModel.Length,
            Width = readModel.Width,
            Height = readModel.Height,
            Volume = readModel.Volume,
            ScannedTime = readModel.ScannedTime,
            DischargeTime = readModel.DischargeTime,
            CompletedTime = readModel.CompletedTime,
            HasImages = readModel.HasImages,
            HasVideos = readModel.HasVideos,
            Coordinate = readModel.Coordinate
        };
    }

    /// <summary>
    /// 将聚合根映射为详情合同。
    /// </summary>
    /// <param name="parcel">包裹聚合根。</param>
    /// <returns>详情合同。</returns>
    public static ParcelDetailResponse ToDetail(Parcel parcel) {
        return new ParcelDetailResponse {
            Id = parcel.Id,
            CreatedTime = parcel.CreatedTime,
            ModifyTime = parcel.ModifyTime,
            ModifyIp = parcel.ModifyIp,
            ParcelTimestamp = parcel.ParcelTimestamp,
            Type = (int)parcel.Type,
            Status = (int)parcel.Status,
            ExceptionType = parcel.ExceptionType is null ? null : (int)parcel.ExceptionType.Value,
            NoReadType = (int)parcel.NoReadType,
            SorterCarrierId = parcel.SorterCarrierId,
            SegmentCodes = parcel.SegmentCodes,
            LifecycleMilliseconds = parcel.LifecycleMilliseconds,
            TargetChuteId = parcel.TargetChuteId,
            ActualChuteId = parcel.ActualChuteId,
            BarCodes = parcel.BarCodes,
            Weight = parcel.Weight,
            RequestStatus = (int)parcel.RequestStatus,
            BagCode = parcel.BagCode,
            WorkstationName = parcel.WorkstationName,
            IsSticking = parcel.IsSticking,
            Length = parcel.Length,
            Width = parcel.Width,
            Height = parcel.Height,
            Volume = parcel.Volume,
            ScannedTime = parcel.ScannedTime,
            DischargeTime = parcel.DischargeTime,
            CompletedTime = parcel.CompletedTime,
            HasImages = parcel.HasImages,
            HasVideos = parcel.HasVideos,
            Coordinate = parcel.Coordinate
        };
    }
}
