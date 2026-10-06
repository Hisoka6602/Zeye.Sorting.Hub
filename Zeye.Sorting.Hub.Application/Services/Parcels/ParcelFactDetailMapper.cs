using System.Text.Json;
using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>从已加载的来源事实和真实摘要补齐缺失详情，不复制明细到数据库。</summary>
internal static class ParcelFactDetailMapper {
    /// <summary>记录旧来源多条码载荷解析异常，避免影响其余详情展示。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>保留既有值对象，仅对缺失分组生成查询投影，兼容历史包裹。</summary>
    public static ParcelDetailResponse Complete(ParcelDetailResponse detail, Parcel parcel) {
        // 步骤1：严格按当前来源身份筛选，未关联及其他包裹的事实不能参与详情。
        var records = parcel.ProcessingRecords.Where(record => record.ParcelId == parcel.Id
            && record.SourceParcelId == parcel.SourceParcelId
            && record.SourceInstanceId == parcel.SourceInstanceId && record.SourceRunId == parcel.SourceRunId)
            .DistinctBy(record => record.RecordId, StringComparer.Ordinal)
            .OrderBy(record => record.OccurredAt).ThenBy(record => record.AttemptNumber)
            .ThenBy(record => record.RecordId, StringComparer.Ordinal).ToArray();
        var measurements = records.Where(record => record.Stage == ParcelProcessingStage.DwsBound
            && record.IsSuccess == true).ToArray();

        // 步骤2：仅使用已有数据生成明细；未知类型、业务结果和时间保留空值。
        return detail with {
            BarCodeInfos = detail.BarCodeInfos.Count > 0 ? detail.BarCodeInfos : BuildBarcodes(detail, records),
            WeightInfos = detail.WeightInfos.Count > 0 ? detail.WeightInfos : BuildWeights(detail, measurements),
            VolumeInfo = detail.VolumeInfo ?? BuildVolume(detail, measurements),
            ChuteInfo = detail.ChuteInfo ?? BuildChute(detail),
            ApiRequests = detail.ApiRequests.Count > 0 ? detail.ApiRequests : BuildApiRequests(records),
            CommandInfos = detail.CommandInfos.Count > 0 ? detail.CommandInfos : BuildCommands(records),
            ImageInfos = detail.ImageInfos.Count > 0 ? detail.ImageInfos : BuildImages(records),
            DeviceInfo = detail.DeviceInfo ?? (string.IsNullOrWhiteSpace(detail.WorkstationName) ? null
                : new ParcelDeviceInfoResponse {
                    WorkstationName = detail.WorkstationName, MachineCode = string.Empty, CustomName = string.Empty
                })
        };
    }

    /// <summary>按条码内容去重，保留最新有效采集身份与时间，同时展示来源的多条码。</summary>
    private static BarCodeInfoResponse[] BuildBarcodes(ParcelDetailResponse detail,
        ParcelProcessingRecord[] records) {
        var barcodes = new Dictionary<string, BarCodeInfoResponse>(StringComparer.Ordinal);
        foreach (var record in records) {
            if (record.Stage == ParcelProcessingStage.DwsBound && record.IsSuccess != true) continue;
            if (record.Stage is not (ParcelProcessingStage.Detected or ParcelProcessingStage.DwsBound
                or ParcelProcessingStage.ScanUploaded)) continue;
            if (!string.IsNullOrWhiteSpace(record.Barcode)) AddBarcode(barcodes, record.Barcode, record);
            if (string.IsNullOrWhiteSpace(record.BarcodesJson)) continue;
            try {
                using var json = JsonDocument.Parse(record.BarcodesJson);
                if (json.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var value in json.RootElement.EnumerateArray()) {
                    if (value.ValueKind == JsonValueKind.String && value.GetString() is { } barcode
                        && !string.IsNullOrWhiteSpace(barcode)) AddBarcode(barcodes, barcode, record);
                }
            }
            catch (JsonException exception) {
                Logger.Warn(exception, "来源多条码格式无效，保留其他包裹详情，RecordId={RecordId}", record.RecordId);
            }
        }
        if (!string.IsNullOrWhiteSpace(detail.BarCodes) && !barcodes.ContainsKey(detail.BarCodes)) {
            barcodes[detail.BarCodes] = new BarCodeInfoResponse {
                BarCode = detail.BarCodes, BarCodeType = null, CapturedTime = null
            };
        }
        return barcodes.Values.ToArray();
    }

    /// <summary>更新真实条码采集观察，不将未知条码类型默认为面单类型。</summary>
    private static void AddBarcode(Dictionary<string, BarCodeInfoResponse> barcodes, string barcode,
        ParcelProcessingRecord record) {
        barcodes[barcode] = new BarCodeInfoResponse {
            BarCode = barcode, BarCodeType = null, RecordId = record.RecordId,
            CapturedTime = record.Stage == ParcelProcessingStage.Detected ? record.OccurredAt : record.MeasuredAt
        };
    }

    /// <summary>展示每次成功绑定的真实称重，将来源克值转换为合同千克值。</summary>
    private static WeightInfoResponse[] BuildWeights(ParcelDetailResponse detail,
        ParcelProcessingRecord[] measurements) {
        var weights = measurements.Where(record => record.WeightGrams.HasValue).Select(record => new WeightInfoResponse {
            RawWeight = string.Empty, EvidenceCode = record.RecordId,
            FormattedWeight = record.WeightGrams!.Value / 1000m, WeighingTime = record.MeasuredAt, AdjustedWeight = null
        }).ToArray();
        if (weights.Length > 0 || !detail.Weight.HasValue) return weights;
        return [new WeightInfoResponse {
            RawWeight = string.Empty, EvidenceCode = string.Empty,
            FormattedWeight = detail.Weight.Value, WeighingTime = detail.MeasurementTime, AdjustedWeight = null
        }];
    }

    /// <summary>复用最新有效尺寸摘要，保留部分测量空值，将立方毫米转换为立方厘米。</summary>
    private static VolumeInfoResponse? BuildVolume(ParcelDetailResponse detail,
        ParcelProcessingRecord[] measurements) {
        if (detail.Length is null && detail.Width is null && detail.Height is null && detail.Volume is null) return null;
        var source = measurements.LastOrDefault(record => record.LengthMm.HasValue || record.WidthMm.HasValue
            || record.HeightMm.HasValue || record.VolumeMm3.HasValue);
        return new VolumeInfoResponse {
            SourceType = null, RawVolume = string.Empty, EvidenceCode = source?.RecordId ?? string.Empty,
            FormattedLength = detail.Length, FormattedWidth = detail.Width, FormattedHeight = detail.Height,
            FormattedVolume = detail.Volume / 1000m,
            AdjustedLength = null, AdjustedWidth = null, AdjustedHeight = null, AdjustedVolume = null,
            MeasurementTime = source is null ? detail.MeasurementTime : source.MeasuredAt, BindTime = source?.OccurredAt
        };
    }

    /// <summary>展示真实目标和落格摘要，编码保持原文，尚未落格时不生成时间。</summary>
    private static ChuteInfoResponse? BuildChute(ParcelDetailResponse detail) {
        if (detail.TargetChuteId is null && detail.ActualChuteId is null && detail.TargetChuteCode is null
            && detail.ActualChuteCode is null && detail.DischargeTime is null) return null;
        return new ChuteInfoResponse {
            TargetChuteId = detail.TargetChuteId, ActualChuteId = detail.ActualChuteId, BackupChuteId = null,
            TargetChuteCode = detail.TargetChuteCode, ActualChuteCode = detail.ActualChuteCode,
            LandedTime = detail.DischargeTime
        };
    }

    /// <summary>逐条保留外部操作和重试，传输状态不能代替未知的业务成功结果。</summary>
    private static ApiRequestInfoResponse[] BuildApiRequests(ParcelProcessingRecord[] records) {
        return records.Where(record => record.Stage is ParcelProcessingStage.ScanUploaded or ParcelProcessingStage.LandingReported)
            .Select(record => new ApiRequestInfoResponse {
                ApiType = record.Stage == ParcelProcessingStage.LandingReported ? (int)ApiRequestType.DischargeReport : null,
                RequestStatus = record.IsSuccess.HasValue
                    ? (int)(record.IsSuccess.Value ? ApiRequestStatus.Success : ApiRequestStatus.Failed) : null,
                RequestUrl = record.RequestUrl ?? string.Empty, QueryParams = string.Empty,
                Headers = record.RequestHeaders ?? string.Empty,
                RequestBody = record.RequestBody ?? string.Empty, ResponseBody = record.ResponseBody ?? string.Empty,
                RequestTime = record.RequestAt, ResponseTime = record.ResponseAt, ElapsedMilliseconds = record.ElapsedMilliseconds,
                Exception = record.ErrorMessage ?? string.Empty, RawData = record.RawPayload ?? string.Empty,
                FormattedMessage = record.DecisionReason ?? string.Empty,
                RecordId = record.RecordId, Provider = record.Provider, Stage = (int)record.Stage,
                AttemptNumber = record.AttemptNumber, ResponseStatusCode = record.ResponseStatusCode, OccurredAt = record.OccurredAt
            }).ToArray();
    }

    /// <summary>展示实际指令下发事实，保持成功、失败及设备协议未知的区别。</summary>
    private static CommandInfoResponse[] BuildCommands(ParcelProcessingRecord[] records) {
        return records.Where(record => record.Stage == ParcelProcessingStage.SorterDispatched).Select(record => new CommandInfoResponse {
            ProtocolType = null, ProtocolName = string.Empty, ConnectionName = string.Empty,
            CommandPayload = record.RawPayload ?? string.Empty, GeneratedTime = record.OccurredAt,
            ActionType = null, Direction = (int)CommandDirection.Send, FormattedMessage = record.DecisionReason ?? string.Empty,
            RecordId = record.RecordId, DispatchedChuteCode = record.DispatchedChuteCode,
            IsSuccess = record.IsSuccess, ErrorMessage = record.ErrorMessage
        }).ToArray();
    }

    /// <summary>按真实图片路径合并登记与上传元数据，未知图片类型和采集方式保留空值。</summary>
    private static ImageInfoResponse[] BuildImages(ParcelProcessingRecord[] records) {
        var images = new Dictionary<string, ImageInfoResponse>(StringComparer.Ordinal);
        foreach (var record in records) {
            if (record.Stage is not (ParcelProcessingStage.ImageRegistered or ParcelProcessingStage.ImageUploaded)
                || record.IsSuccess == false || string.IsNullOrWhiteSpace(record.ImagePath)) continue;
            images.TryGetValue(record.ImagePath, out var previous);
            images[record.ImagePath] = new ImageInfoResponse {
                CameraName = record.ImageCamera ?? previous?.CameraName ?? string.Empty,
                CustomName = string.Empty, CameraSerialNumber = string.Empty,
                ImageType = null, RelativePath = record.ImagePath, CaptureType = null,
                Sha256 = record.ImageContentHash ?? previous?.Sha256
            };
        }
        return images.Values.ToArray();
    }
}
