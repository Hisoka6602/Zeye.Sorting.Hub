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
    /// Parcel 合同映射参数。
    /// </summary>
    private readonly record struct ParcelResponseArgs {
        /// <summary>
        /// 包裹 Id。
        /// </summary>
        public required long Id { get; init; }

        /// <summary>
        /// 创建时间。
        /// </summary>
        public required DateTime CreatedTime { get; init; }

        /// <summary>
        /// 修改时间。
        /// </summary>
        public required DateTime ModifyTime { get; init; }

        /// <summary>
        /// 修改 IP。
        /// </summary>
        public required string ModifyIp { get; init; }

        /// <summary>
        /// 包裹时间戳。
        /// </summary>
        public required long ParcelTimestamp { get; init; }

        /// <summary>
        /// 包裹类型。
        /// </summary>
        public required int Type { get; init; }

        /// <summary>
        /// 包裹状态。
        /// </summary>
        public required int Status { get; init; }

        /// <summary>
        /// 包裹异常类型。
        /// </summary>
        public required int? ExceptionType { get; init; }

        /// <summary>
        /// NoRead 类型。
        /// </summary>
        public required int NoReadType { get; init; }

        /// <summary>
        /// 小车编号。
        /// </summary>
        public required int? SorterCarrierId { get; init; }

        /// <summary>
        /// 三段码。
        /// </summary>
        public required string? SegmentCodes { get; init; }

        /// <summary>
        /// 生命周期（毫秒）。
        /// </summary>
        public required long? LifecycleMilliseconds { get; init; }

        /// <summary>
        /// 目标格口 Id。
        /// </summary>
        public required long TargetChuteId { get; init; }

        /// <summary>
        /// 实际格口 Id。
        /// </summary>
        public required long ActualChuteId { get; init; }

        /// <summary>
        /// 包裹主条码。
        /// </summary>
        public required string BarCodes { get; init; }

        /// <summary>
        /// 重量。
        /// </summary>
        public required decimal Weight { get; init; }

        /// <summary>
        /// 外部接口访问状态。
        /// </summary>
        public required int RequestStatus { get; init; }

        /// <summary>
        /// 集包号。
        /// </summary>
        public required string BagCode { get; init; }

        /// <summary>
        /// 工作台名称。
        /// </summary>
        public required string WorkstationName { get; init; }

        /// <summary>
        /// 是否叠包。
        /// </summary>
        public required bool IsSticking { get; init; }

        /// <summary>
        /// 长度。
        /// </summary>
        public required decimal Length { get; init; }

        /// <summary>
        /// 宽度。
        /// </summary>
        public required decimal Width { get; init; }

        /// <summary>
        /// 高度。
        /// </summary>
        public required decimal Height { get; init; }

        /// <summary>
        /// 体积。
        /// </summary>
        public required decimal Volume { get; init; }

        /// <summary>
        /// 扫码时间。
        /// </summary>
        public required DateTime ScannedTime { get; init; }

        /// <summary>
        /// 落格时间。
        /// </summary>
        public required DateTime DischargeTime { get; init; }

        /// <summary>
        /// 包裹完结时间。
        /// </summary>
        public required DateTime? CompletedTime { get; init; }

        /// <summary>
        /// 是否有图片。
        /// </summary>
        public required bool HasImages { get; init; }

        /// <summary>
        /// 是否有视频。
        /// </summary>
        public required bool HasVideos { get; init; }

        /// <summary>
        /// 包裹坐标。
        /// </summary>
        public required string Coordinate { get; init; }
    }

    /// <summary>
    /// 将摘要读模型映射为列表项合同。
    /// </summary>
    /// <param name="readModel">仓储摘要读模型。</param>
    /// <returns>列表项合同。</returns>
    public static ParcelListItemResponse ToListItem(ParcelSummaryReadModel readModel) {
        var args = BuildFromReadModel(readModel);
        return CreateParcelListItemResponse(args);
    }

    /// <summary>
    /// 将聚合根映射为详情合同。
    /// </summary>
    /// <param name="parcel">包裹聚合根。</param>
    /// <returns>详情合同。</returns>
    public static ParcelDetailResponse ToDetail(Parcel parcel) {
        var args = BuildFromAggregate(parcel);
        var listItem = CreateParcelListItemResponse(args);
        return new ParcelDetailResponse(
            listItem,
            barCodeInfos: parcel.BarCodeInfos.Select(x => new BarCodeInfoResponse {
                BarCode = x.BarCode,
                BarCodeType = (int)x.BarCodeType,
                CapturedTime = x.CapturedTime
            }).ToList(),
            weightInfos: parcel.WeightInfos.Select(x => new WeightInfoResponse {
                RawWeight = x.RawWeight,
                EvidenceCode = x.EvidenceCode,
                FormattedWeight = x.FormattedWeight,
                WeighingTime = x.WeighingTime,
                AdjustedWeight = x.AdjustedWeight
            }).ToList(),
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
                RequestTime = x.RequestTime,
                ResponseTime = x.ResponseTime,
                ElapsedMilliseconds = x.ElapsedMilliseconds,
                Exception = x.Exception,
                FormattedMessage = x.FormattedMessage
            }).ToList(),
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
            }).ToList(),
            imageInfos: parcel.ImageInfos.Select(x => new ImageInfoResponse {
                CameraName = x.CameraName,
                CustomName = x.CustomName,
                CameraSerialNumber = x.CameraSerialNumber,
                ImageType = (int)x.ImageType,
                RelativePath = x.RelativePath,
                CaptureType = (int)x.CaptureType
            }).ToList(),
            videoInfos: parcel.VideoInfos.Select(x => new VideoInfoResponse {
                Channel = x.Channel,
                NvrSerialNumber = x.NvrSerialNumber,
                NodeType = (int)x.NodeType
            }).ToList(),
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
    /// 从摘要读模型构建映射参数。
    /// </summary>
    /// <param name="readModel">摘要读模型。</param>
    /// <returns>映射参数对象。</returns>
    private static ParcelResponseArgs BuildFromReadModel(ParcelSummaryReadModel readModel) {
        return new ParcelResponseArgs {
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
    /// 从聚合根构建映射参数。
    /// </summary>
    /// <param name="parcel">包裹聚合根。</param>
    /// <returns>映射参数对象。</returns>
    private static ParcelResponseArgs BuildFromAggregate(Parcel parcel) {
        return new ParcelResponseArgs {
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
