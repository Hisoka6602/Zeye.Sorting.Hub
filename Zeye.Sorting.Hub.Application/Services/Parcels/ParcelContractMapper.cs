using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;
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
        return CreateParcelListItemResponse(BuildFrom(readModel));
    }

    /// <summary>
    /// 将聚合根映射为详情合同。
    /// </summary>
    /// <param name="parcel">包裹聚合根。</param>
    /// <returns>详情合同。</returns>
    public static ParcelDetailResponse ToDetail(Parcel parcel) {
        var listItem = CreateParcelListItemResponse(BuildFrom(parcel));
        return new ParcelDetailResponse(
            listItem,
            barCodeInfos: parcel.BarCodeInfos.Select(x => new BarCodeInfoResponse {
                BarCode = x.BarCode,
                BarCodeType = (int)x.BarCodeType,
                CapturedTime = x.CapturedTime
            }).ToArray(),
            weightInfos: parcel.WeightInfos.Select(x => new WeightInfoResponse {
                RawWeight = x.RawWeight,
                EvidenceCode = x.EvidenceCode,
                FormattedWeight = x.FormattedWeight,
                WeighingTime = x.WeighingTime,
                AdjustedWeight = x.AdjustedWeight
            }).ToArray(),
            volumeInfo: parcel.VolumeInfo is null ? null : new VolumeInfoResponse {
                SourceType = (int)parcel.VolumeInfo.SourceType,
                RawVolume = parcel.VolumeInfo.RawVolume,
                EvidenceCode = parcel.VolumeInfo.EvidenceCode,
                FormattedLength = parcel.VolumeInfo.FormattedLength,
                FormattedWidth = parcel.VolumeInfo.FormattedWidth,
                FormattedHeight = parcel.VolumeInfo.FormattedHeight,
                FormattedVolume = parcel.VolumeInfo.FormattedVolume,
                AdjustedLength = parcel.VolumeInfo.AdjustedLength,
                AdjustedWidth = parcel.VolumeInfo.AdjustedWidth,
                AdjustedHeight = parcel.VolumeInfo.AdjustedHeight,
                AdjustedVolume = parcel.VolumeInfo.AdjustedVolume,
                MeasurementTime = parcel.VolumeInfo.MeasurementTime,
                BindTime = parcel.VolumeInfo.BindTime
            },
            apiRequests: parcel.ApiRequests.Select(x => new ApiRequestInfoResponse {
                ApiType = (int)x.ApiType,
                RequestStatus = (int)x.RequestStatus,
                RequestUrl = x.RequestUrl,
                QueryParams = x.QueryParams,
                Headers = x.Headers,
                RequestBody = x.RequestBody,
                ResponseBody = x.ResponseBody,
                RequestTime = x.RequestTime,
                ResponseTime = x.ResponseTime,
                ElapsedMilliseconds = x.ElapsedMilliseconds,
                Exception = x.Exception,
                RawData = x.RawData,
                FormattedMessage = x.FormattedMessage
            }).ToArray(),
            chuteInfo: parcel.ChuteInfo is null ? null : new ChuteInfoResponse {
                TargetChuteId = parcel.ChuteInfo.TargetChuteId,
                ActualChuteId = parcel.ChuteInfo.ActualChuteId,
                BackupChuteId = parcel.ChuteInfo.BackupChuteId,
                LandedTime = parcel.ChuteInfo.LandedTime
            },
            commandInfos: parcel.CommandInfos.Select(x => new CommandInfoResponse {
                ProtocolType = (int)x.ProtocolType,
                ProtocolName = x.ProtocolName,
                ConnectionName = x.ConnectionName,
                CommandPayload = x.CommandPayload,
                GeneratedTime = x.GeneratedTime,
                ActionType = (int)x.ActionType,
                FormattedMessage = x.FormattedMessage,
                Direction = (int)x.Direction
            }).ToArray(),
            imageInfos: parcel.ImageInfos.Select(x => new ImageInfoResponse {
                CameraName = x.CameraName,
                CustomName = x.CustomName,
                CameraSerialNumber = x.CameraSerialNumber,
                ImageType = (int)x.ImageType,
                RelativePath = x.RelativePath,
                CaptureType = (int)x.CaptureType
            }).ToArray(),
            videoInfos: parcel.VideoInfos.Select(x => new VideoInfoResponse {
                Channel = x.Channel,
                NvrSerialNumber = x.NvrSerialNumber,
                NodeType = (int)x.NodeType
            }).ToArray(),
            sorterCarrierInfo: parcel.SorterCarrierInfo is null ? null : new SorterCarrierInfoResponse {
                SorterCarrierId = parcel.SorterCarrierInfo.SorterCarrierId,
                LoadedTime = parcel.SorterCarrierInfo.LoadedTime,
                ConveyorSpeedWhenLoaded = parcel.SorterCarrierInfo.ConveyorSpeedWhenLoaded,
                LinkedCarrierCount = parcel.SorterCarrierInfo.LinkedCarrierCount
            },
            bagInfo: parcel.BagInfo is null ? null : new BagInfoResponse {
                ChuteId = parcel.BagInfo.ChuteId,
                ChuteName = parcel.BagInfo.ChuteName,
                BagCode = parcel.BagInfo.BagCode,
                ParcelCount = parcel.BagInfo.ParcelCount,
                BaggingTime = parcel.BagInfo.BaggingTime
            },
            deviceInfo: parcel.DeviceInfo is null ? null : new ParcelDeviceInfoResponse {
                WorkstationName = parcel.DeviceInfo.WorkstationName,
                MachineCode = parcel.DeviceInfo.MachineCode,
                CustomName = parcel.DeviceInfo.CustomName
            },
            grayDetectorInfo: parcel.GrayDetectorInfo is null ? null : new GrayDetectorInfoResponse {
                CarrierNumber = parcel.GrayDetectorInfo.CarrierNumber,
                AttachBoxInfo = parcel.GrayDetectorInfo.AttachBoxInfo,
                MainBoxInfo = parcel.GrayDetectorInfo.MainBoxInfo,
                LinkedCarrierCount = parcel.GrayDetectorInfo.LinkedCarrierCount,
                CenterPosition = parcel.GrayDetectorInfo.CenterPosition,
                ResultTime = parcel.GrayDetectorInfo.ResultTime,
                RawResult = parcel.GrayDetectorInfo.RawResult
            },
            stickingParcelInfo: parcel.StickingParcelInfo is null ? null : new StickingParcelInfoResponse {
                IsSticking = parcel.StickingParcelInfo.IsSticking,
                ReceiveTime = parcel.StickingParcelInfo.ReceiveTime,
                RawData = parcel.StickingParcelInfo.RawData,
                ElapsedMilliseconds = parcel.StickingParcelInfo.ElapsedMilliseconds
            },
            parcelPositionInfo: parcel.ParcelPositionInfo is null ? null : new ParcelPositionInfoResponse {
                X1 = parcel.ParcelPositionInfo.X1,
                X2 = parcel.ParcelPositionInfo.X2,
                Y1 = parcel.ParcelPositionInfo.Y1,
                Y2 = parcel.ParcelPositionInfo.Y2,
                BackgroundX1 = parcel.ParcelPositionInfo.BackgroundX1,
                BackgroundX2 = parcel.ParcelPositionInfo.BackgroundX2,
                BackgroundY1 = parcel.ParcelPositionInfo.BackgroundY1,
                BackgroundY2 = parcel.ParcelPositionInfo.BackgroundY2
            }
        );
    }

    /// <summary>
    /// 从实现了 IParcelSummaryView 的对象构建映射参数（统一消费 Parcel 聚合根与 ParcelSummaryReadModel）。
    /// </summary>
    /// <param name="view">Parcel 摘要视图。</param>
    /// <returns>映射参数对象。</returns>
    private static ParcelResponseArgs BuildFrom(IParcelSummaryView view) {
        return new ParcelResponseArgs {
            Id = view.Id,
            CreatedTime = view.CreatedTime,
            ModifyTime = view.ModifyTime,
            ModifyIp = view.ModifyIp,
            ParcelTimestamp = view.ParcelTimestamp,
            Type = (int)view.Type,
            Status = (int)view.Status,
            ExceptionType = view.ExceptionType is null ? null : (int)view.ExceptionType.Value,
            NoReadType = (int)view.NoReadType,
            SorterCarrierId = view.SorterCarrierId,
            SegmentCodes = view.SegmentCodes,
            LifecycleMilliseconds = view.LifecycleMilliseconds,
            TargetChuteId = view.TargetChuteId,
            ActualChuteId = view.ActualChuteId,
            BarCodes = view.BarCodes,
            Weight = view.Weight,
            RequestStatus = (int)view.RequestStatus,
            BagCode = view.BagCode,
            WorkstationName = view.WorkstationName,
            IsSticking = view.IsSticking,
            Length = view.Length,
            Width = view.Width,
            Height = view.Height,
            Volume = view.Volume,
            ScannedTime = view.ScannedTime,
            DischargeTime = view.DischargeTime,
            CompletedTime = view.CompletedTime,
            HasImages = view.HasImages,
            HasVideos = view.HasVideos,
            Coordinate = view.Coordinate
        };
    }

    /// <summary>
    /// 创建 Parcel 列表项合同对象。
    /// </summary>
    /// <param name="args">映射参数对象。</param>
    /// <returns>列表项合同。</returns>
    private static ParcelListItemResponse CreateParcelListItemResponse(ParcelResponseArgs args) {
        return new ParcelListItemResponse {
            Id = args.Id,
            CreatedTime = args.CreatedTime,
            ModifyTime = args.ModifyTime,
            ModifyIp = args.ModifyIp,
            ParcelTimestamp = args.ParcelTimestamp,
            Type = args.Type,
            Status = args.Status,
            ExceptionType = args.ExceptionType,
            NoReadType = args.NoReadType,
            SorterCarrierId = args.SorterCarrierId,
            SegmentCodes = args.SegmentCodes,
            LifecycleMilliseconds = args.LifecycleMilliseconds,
            TargetChuteId = args.TargetChuteId,
            ActualChuteId = args.ActualChuteId,
            BarCodes = args.BarCodes,
            Weight = args.Weight,
            RequestStatus = args.RequestStatus,
            BagCode = args.BagCode,
            WorkstationName = args.WorkstationName,
            IsSticking = args.IsSticking,
            Length = args.Length,
            Width = args.Width,
            Height = args.Height,
            Volume = args.Volume,
            ScannedTime = args.ScannedTime,
            DischargeTime = args.DischargeTime,
            CompletedTime = args.CompletedTime,
            HasImages = args.HasImages,
            HasVideos = args.HasVideos,
            Coordinate = args.Coordinate
        };
    }
}
