using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证来源事实在历史包裹详情中的完整投影与无写入边界。</summary>
public sealed class ParcelFactDetailTests {
    /// <summary>隔离测试所用的固定本地时间。</summary>
    private static readonly DateTime StartedAt = new(2026, 10, 6, 10, 0, 0);

    /// <summary>真实数据库历史记录补齐全部已上报分组，查询不会复制附属表数据。</summary>
    [Fact]
    public async Task PersistedHistoryPopulatesDetailsWithoutWritingAdditionalRows() {
        await using var database = new RelationalParcelTestDatabase("PerMonth");
        await database.InitializeAsync();
        var detection = Fact("detected", ParcelProcessingStage.Detected, 0);
        var created = await database.Processing.AppendAsync(detection, default);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        var parcelId = created.Value!.ParcelId!.Value;
        var records = new[] {
            Fact("measurement", ParcelProcessingStage.DwsBound, 1) with {
                WeightGrams = 1120m, LengthMm = 300m, WidthMm = 200m, HeightMm = 100m,
                VolumeMm3 = 6000000m, MeasuredAt = StartedAt.AddMilliseconds(950), FinalSourceParcelId = 21
            },
            Fact("remeasurement", ParcelProcessingStage.DwsBound, 2) with {
                WeightGrams = 1180m, MeasuredAt = StartedAt.AddMilliseconds(1950), FinalSourceParcelId = 21
            },
            Fact("upload-failure", ParcelProcessingStage.ScanUploaded, 3) with {
                IsSuccess = false, Provider = "test-provider", ErrorMessage = "业务拒绝",
                RequestUrl = "https://provider.invalid/scan", RequestHeaders = "header",
                RequestBody = "request", ResponseBody = "failure", ResponseStatusCode = 400,
                RequestAt = StartedAt.AddMilliseconds(2900), ResponseAt = StartedAt.AddSeconds(3), ElapsedMilliseconds = 100
            },
            Fact("upload-unknown", ParcelProcessingStage.ScanUploaded, 4) with {
                IsSuccess = null, AttemptNumber = 2, ResponseStatusCode = 200
            },
            Fact("upload-success", ParcelProcessingStage.ScanUploaded, 5) with {
                AttemptNumber = 3, ResponseBody = "accepted", BarcodesJson = "[\"PRIMARY\",\"SECONDARY\",\"SECONDARY\"]"
            },
            Fact("route", ParcelProcessingStage.ChuteAssigned, 6) with { TargetChuteCode = "0013" },
            Fact("dispatch", ParcelProcessingStage.SorterDispatched, 7) with { DispatchedChuteCode = "0013", RawPayload = "AA BB" },
            Fact("landed", ParcelProcessingStage.SortingCompleted, 8) with { ActualChuteCode = "13" },
            Fact("image-one", ParcelProcessingStage.ImageRegistered, 9) with { ImagePath = "images/one.jpg", ImageCamera = "顶视相机" },
            Fact("image-one-upload", ParcelProcessingStage.ImageUploaded, 10) with { ImagePath = "images/one.jpg", ImageContentHash = "hash-one" },
            Fact("image-two", ParcelProcessingStage.ImageRegistered, 11) with { ImagePath = "images/two.jpg", ImageCamera = "侧视相机" }
        };
        foreach (var record in records) {
            var appended = await database.Processing.AppendAsync(record, default);
            Assert.True(appended.IsSuccess, appended.ErrorMessage);
        }

        var query = new GetParcelByIdQueryService(database.Parcels);
        var detail = await query.ExecuteAsync(parcelId, default);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.BarCodeInfos.Count);
        Assert.Contains(detail.BarCodeInfos, barcode => barcode.BarCode == "SECONDARY");
        Assert.Equal([1.12m, 1.18m], detail.WeightInfos.Select(weight => weight.FormattedWeight));
        Assert.Equal(detail.Weight, detail.WeightInfos[^1].FormattedWeight);
        Assert.NotNull(detail.VolumeInfo);
        Assert.Equal(detail.Length, detail.VolumeInfo.FormattedLength);
        Assert.Equal(6000m, detail.VolumeInfo.FormattedVolume);
        Assert.Equal("measurement", detail.VolumeInfo.EvidenceCode);
        Assert.Equal(StartedAt.AddMilliseconds(950), detail.VolumeInfo.MeasurementTime);
        Assert.Equal("0013", detail.ChuteInfo!.TargetChuteCode);
        Assert.Equal("13", detail.ChuteInfo.ActualChuteCode);
        Assert.Equal(detail.DischargeTime, detail.ChuteInfo.LandedTime);
        Assert.Equal(3, detail.ApiRequests.Count);
        Assert.Equal((int)ApiRequestStatus.Failed, detail.ApiRequests[0].RequestStatus);
        Assert.Equal("业务拒绝", detail.ApiRequests[0].Exception);
        Assert.Equal("request", detail.ApiRequests[0].RequestBody);
        Assert.Equal("header", detail.ApiRequests[0].Headers);
        Assert.Null(detail.ApiRequests[1].RequestStatus);
        Assert.Equal(200, detail.ApiRequests[1].ResponseStatusCode);
        Assert.Equal(3, detail.ApiRequests[2].AttemptNumber);
        Assert.Equal((int)ApiRequestStatus.Success, detail.ApiRequests[2].RequestStatus);
        Assert.Equal("AA BB", Assert.Single(detail.CommandInfos).CommandPayload);
        Assert.Equal("0013", detail.CommandInfos[0].DispatchedChuteCode);
        Assert.Null(detail.CommandInfos[0].ProtocolType);
        Assert.Equal(2, detail.ImageInfos.Count);
        Assert.Equal("顶视相机", detail.ImageInfos[0].CameraName);
        Assert.Equal("hash-one", detail.ImageInfos[0].Sha256);
        Assert.Equal(detail.WorkstationName, detail.DeviceInfo!.WorkstationName);

        var repeated = await query.ExecuteAsync(parcelId, default);
        Assert.Equal(detail.WeightInfos, repeated!.WeightInfos);
        Assert.Equal(12, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202610"));
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_202610"));
        foreach (var table in new[] { "Parcel_BarCodeInfos", "Parcel_WeightInfos", "Parcel_VolumeInfos", "Parcel_ApiRequests",
            "Parcel_ChuteInfos", "Parcel_CommandInfos", "Parcel_ImageInfos", "Parcel_DeviceInfos" }) {
            Assert.Equal(0, await database.CountPhysicalAsync(table + "_202610"));
        }
    }

    /// <summary>未知尺寸、类型、请求时间和未落格时间保持为空，显式零值仍可区分。</summary>
    [Fact]
    public async Task PartialMeasurementsAndPendingOperationsPreserveUnknownFields() {
        await using var database = new RelationalParcelTestDatabase("PerMonth");
        await database.InitializeAsync();
        var created = await database.Processing.AppendAsync(Fact("detected", ParcelProcessingStage.Detected, 0), default);
        foreach (var record in new[] {
            Fact("partial", ParcelProcessingStage.DwsBound, 1) with { WeightGrams = 1120m, LengthMm = 300m, HeightMm = 0m, FinalSourceParcelId = 21 },
            Fact("operation", ParcelProcessingStage.ScanUploaded, 2) with { IsSuccess = null, ResponseStatusCode = 200 },
            Fact("route", ParcelProcessingStage.ChuteAssigned, 3) with { TargetChuteCode = "A01-01" }
        }) {
            Assert.True((await database.Processing.AppendAsync(record, default)).IsSuccess);
        }
        var detail = await new GetParcelByIdQueryService(database.Parcels).ExecuteAsync(created.Value!.ParcelId!.Value, default);
        Assert.NotNull(detail);
        Assert.Null(detail.BarCodeInfos[0].BarCodeType);
        Assert.Null(detail.VolumeInfo!.SourceType);
        Assert.Null(detail.VolumeInfo.FormattedWidth);
        Assert.Equal(0m, detail.VolumeInfo.FormattedHeight);
        Assert.Null(detail.VolumeInfo.FormattedVolume);
        Assert.Null(detail.VolumeInfo.MeasurementTime);
        Assert.Empty(detail.VolumeInfo.RawVolume);
        Assert.Null(Assert.Single(detail.WeightInfos).WeighingTime);
        Assert.Empty(detail.WeightInfos[0].RawWeight);
        Assert.Equal("partial", detail.WeightInfos[0].EvidenceCode);
        Assert.Equal("A01-01", detail.ChuteInfo!.TargetChuteCode);
        Assert.Null(detail.ChuteInfo.TargetChuteId);
        Assert.Null(detail.ChuteInfo.ActualChuteCode);
        Assert.Null(detail.ChuteInfo.LandedTime);
        var operation = Assert.Single(detail.ApiRequests);
        Assert.Null(operation.ApiType);
        Assert.Null(operation.RequestStatus);
        Assert.Null(operation.RequestTime);
        Assert.Null(operation.ResponseTime);
        Assert.Null(operation.ElapsedMilliseconds);
        Assert.Equal(StartedAt.AddSeconds(2), operation.OccurredAt);
        Assert.Empty(detail.CommandInfos);
        Assert.Empty(detail.ImageInfos);
        Assert.Empty(detail.VideoInfos);
        Assert.Null(detail.ParcelPositionInfo);
    }

    /// <summary>已有值对象保留原始证据、类型和调整值，不被查询补齐覆盖。</summary>
    [Fact]
    public async Task ExistingOwnedDetailsRemainAuthoritative() {
        var parcel = Parcel.CreateDetected(91, Fact("detected", ParcelProcessingStage.Detected, 0), StartedAt);
        parcel.ApplyProcessingRecords([Fact("measurement", ParcelProcessingStage.DwsBound, 1) with {
            ParcelId = 91, WeightGrams = 1120m, LengthMm = 300m, WidthMm = 200m, HeightMm = 100m,
            VolumeMm3 = 6000000m, FinalSourceParcelId = 21
        }]);
        parcel.AddBarCodeInfo(new BarCodeInfo { BarCode = "OWNED", BarCodeType = BarCodeType.Product, CapturedTime = StartedAt });
        parcel.AddWeightInfo(new WeightInfo {
            RawWeight = "scale-frame", EvidenceCode = "scale-001", FormattedWeight = 1.15m,
            AdjustedWeight = 1.12m, WeighingTime = StartedAt
        });
        parcel.SetVolumeInfo(new VolumeInfo {
            SourceType = VolumeSourceType.Sensor, RawVolume = "dimension-frame", EvidenceCode = "volume-001",
            FormattedLength = 300m, FormattedWidth = 200m, FormattedHeight = 100m, FormattedVolume = 6000m,
            MeasurementTime = StartedAt
        });
        var detail = await QuerySnapshotAsync(parcel);
        Assert.Equal("OWNED", Assert.Single(detail.BarCodeInfos).BarCode);
        Assert.Equal((int)BarCodeType.Product, detail.BarCodeInfos[0].BarCodeType);
        Assert.Equal("scale-frame", Assert.Single(detail.WeightInfos).RawWeight);
        Assert.Equal(1.12m, detail.WeightInfos[0].AdjustedWeight);
        Assert.Equal("dimension-frame", detail.VolumeInfo!.RawVolume);
        Assert.Equal((int)VolumeSourceType.Sensor, detail.VolumeInfo.SourceType);
    }

    /// <summary>其他来源会话、未绑定报文及重复事实不会污染当前包裹明细。</summary>
    [Fact]
    public async Task DetailProjectionRespectsIdentityAndDeduplicatesReplays() {
        var parcel = Parcel.CreateDetected(92, Fact("detected", ParcelProcessingStage.Detected, 0), StartedAt);
        var measurement = Fact("valid", ParcelProcessingStage.DwsBound, 1) with {
            ParcelId = 92, WeightGrams = 1120m, FinalSourceParcelId = 21
        };
        parcel.ApplyProcessingRecords([measurement]);
        parcel.LoadProcessingRecords([
            measurement, measurement,
            measurement with { RecordId = "other-instance", SourceInstanceId = "other", WeightGrams = 99999m, Barcode = "FOREIGN" },
            measurement with { RecordId = "other-run", SourceRunId = "other", WeightGrams = 99999m },
            measurement with { RecordId = "other-parcel", SourceParcelId = 22, WeightGrams = 99999m },
            measurement with { RecordId = "unbound", Stage = ParcelProcessingStage.DwsReceived, SourceParcelId = null, WeightGrams = 99999m }
        ]);
        var detail = await QuerySnapshotAsync(parcel);
        Assert.Equal(1.12m, Assert.Single(detail.WeightInfos).FormattedWeight);
        Assert.DoesNotContain(detail.BarCodeInfos, barcode => barcode.BarCode == "FOREIGN");
        Assert.Empty(parcel.WeightInfos);
        Assert.Empty(parcel.BarCodeInfos);
    }

    /// <summary>只有主表摘要的旧包裹仍展示已知条码和量测，不制造图片或接口记录。</summary>
    [Fact]
    public async Task LegacySummaryOnlyParcelsExposeKnownValuesWithoutInventedHistory() {
        var detail = await new GetParcelByIdQueryService(new FakeParcelRepository()).ExecuteAsync(1, default);
        Assert.NotNull(detail);
        Assert.Contains(detail.BarCodeInfos, barcode => barcode.BarCode == detail.BarCodes);
        Assert.Equal(detail.Weight, Assert.Single(detail.WeightInfos).FormattedWeight);
        Assert.Equal(detail.Volume / 1000m, detail.VolumeInfo!.FormattedVolume);
        Assert.Equal(detail.TargetChuteId, detail.ChuteInfo!.TargetChuteId);
        Assert.Equal(detail.DischargeTime, detail.ChuteInfo.LandedTime);
        Assert.Empty(detail.ProcessingRecords);
        Assert.Empty(detail.ApiRequests);
        Assert.Empty(detail.ImageInfos);
    }

    /// <summary>旧来源多条码格式损坏不会导致包裹详情整体失败。</summary>
    [Fact]
    public async Task InvalidHistoricalBarcodeJsonKeepsPrimaryBarcodeAndOtherDetails() {
        var parcel = Parcel.CreateDetected(93, Fact("detected", ParcelProcessingStage.Detected, 0), StartedAt);
        parcel.ApplyProcessingRecords([
            Fact("detected", ParcelProcessingStage.Detected, 0) with { ParcelId = 93 },
            Fact("upload", ParcelProcessingStage.ScanUploaded, 1) with { ParcelId = 93, BarcodesJson = "invalid-json" }
        ]);
        var detail = await QuerySnapshotAsync(parcel);
        Assert.Equal("PRIMARY", Assert.Single(detail.BarCodeInfos).BarCode);
        Assert.Single(detail.ApiRequests);
    }

    /// <summary>通过公开查询服务执行聚合快照映射，复用已有仓储替身。</summary>
    private static async Task<ParcelDetailResponse> QuerySnapshotAsync(Parcel parcel) {
        var repository = new FakeParcelRepository();
        Assert.True((await repository.AddAsync(parcel, default)).IsSuccess);
        var detail = await new GetParcelByIdQueryService(repository).ExecuteAsync(parcel.Id, default);
        Assert.NotNull(detail);
        return detail;
    }

    /// <summary>构造稳定来源身份、真实阶段与本地发生时间的处理事实。</summary>
    private static ParcelProcessingRecord Fact(string recordId, ParcelProcessingStage stage, int seconds) => new() {
        RecordId = recordId, SourceInstanceId = "detail-source", SourceRunId = "detail-run", SourceParcelId = 21,
        WorkstationName = "详情验证工作台", Barcode = "PRIMARY", Stage = stage, IsSuccess = true,
        OccurredAt = StartedAt.AddSeconds(seconds), RecordedAt = StartedAt.AddSeconds(seconds),
        PartitionTime = StartedAt, PayloadHash = "detail-test"
    };
}
