using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Tools.BusinessDataSimulator;

/// <summary>确定性模拟分拣链路：工作日件量、到件批次、四台设备、失败重试和未知异常。</summary>
public static class SimulationScenario {
    /// <summary>仅供造数工具使用的来源标记。</summary>
    public const string Marker = "zeye-business-simulation-v1";
    /// <summary>与现有8、9开头的演示编号隔离。</summary>
    public static long Id(char prefix, DateTime day, int sequence) => long.Parse($"{prefix}{day:yyyyMMdd}{sequence:D4}", CultureInfo.InvariantCulture);
    /// <summary>沿用真实处理仓储的来源身份编码方式。</summary>
    public static string IdentityHash(params string[] parts) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));

    /// <summary>生成精确总量，既包含吞吐批次，也保留工作间歇。</summary>
    public static SimulationParcel[] Generate(SimulationOptions options) {
        options.Validate();
        var days = Enumerable.Range(0, options.Days).Select(i => options.Start.AddDays(i)).ToArray();
        var dailyFactors = new[] { 100, 105, 112, 109, 102, 95, 89, 94 };
        var weights = days.Select((day, index) => (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 72m : 110m) * dailyFactors[index % dailyFactors.Length] * (day == options.AsOf.Date ? 65m : 100m)).ToArray();
        var counts = weights.Select(w => (int)Math.Floor(options.Count * w / weights.Sum())).ToArray();
        for (var i = 0; counts.Sum() < options.Count; i++) counts[i % days.Length]++;
        var result = new List<SimulationParcel>(options.Count);
        for (var d = 0; d < days.Length; d++) {
            var day = days[d];
            var random = new Random(day.Year * 10000 + day.Month * 100 + day.Day + 47017);
            var count = counts[d];
            var pendingCount = day == options.AsOf.Date ? Math.Min(8, count / 10) : 0;
            var perSourceCounter = new int[4];
            var previous = new DateTime?[4];
            var cursors = new DateTime[3];
            for (var burst = 0; burst < 3; burst++) {
                cursors[burst] = day == options.AsOf.Date
                    ? options.AsOf.AddMinutes(-4).AddMilliseconds(-(count + 10) * 1300).AddMilliseconds(burst * (count + 10) * 440)
                    : day.AddMinutes(new[] { 450, 780, 1080 }[burst]);
                if (cursors[burst] < day) cursors[burst] = day.AddMilliseconds(10000 + burst * (count + 10) * 440);
            }
            for (var n = 1; n <= count; n++) {
                var pending = n > count - pendingCount;
                var burst = Math.Min(2, (n - 1) * 3 / (count - pendingCount));
                cursors[burst] = cursors[burst].AddMilliseconds(random.Next(650, 1251));
                var generatedTime = pending ? options.AsOf.AddMilliseconds(-(count - n + 1) * 1100 - 5000) : cursors[burst];
                var time = new DateTime(generatedTime.Ticks - generatedTime.Ticks % 10, DateTimeKind.Unspecified);
                var stationRoll = random.Next(100);
                var station = stationRoll < 35 ? 0 : stationRoll < 65 ? 1 : stationRoll < 87 ? 2 : 3;
                var sourceNumber = ++perSourceCounter[station];
                var gap = previous[station].HasValue ? (time - previous[station]!.Value).Ticks / (long?)TimeSpan.TicksPerMillisecond : null;
                previous[station] = time;
                result.Add(Create(options, day, n, sourceNumber, station, time, gap, pending, random));
            }
        }
        Validate(result, options);
        return result.ToArray();
    }

    /// <summary>生成事实后通过真实领域重放计算快照，附属明细来自同一事实。</summary>
    private static SimulationParcel Create(SimulationOptions options, DateTime day, int sequence, int sourceNumber, int station, DateTime time, long? gap, bool pending, Random random) {
        var id = Id('7', day, sequence);
        var source = $"sim-fusion-{station + 1:D2}";
        var run = $"sim-v1-{day:yyyyMMdd}";
        var workstation = $"模拟工作台 {station + 1}";
        var kindRoll = random.Next(100);
        var kind = kindRoll < 65 ? ParcelType.Normal : kindRoll < 78 ? ParcelType.UltraThin : kindRoll < 88 ? ParcelType.Large : kindRoll < 94 ? ParcelType.Fragile : kindRoll < 98 ? ParcelType.Liquid : ParcelType.Irregular;
        decimal length = random.Next(180, 401), width = random.Next(120, 281), height = random.Next(80, 221);
        if (kind == ParcelType.UltraThin) { length = random.Next(220, 331); width = random.Next(150, 231); height = random.Next(8, 26); }
        if (kind == ParcelType.Large) { length = random.Next(550, 851); width = random.Next(350, 551); height = random.Next(280, 481); }
        if (kind == ParcelType.Liquid) { length = random.Next(230, 371); width = random.Next(170, 251); height = random.Next(210, 321); }
        var volume = length * width * height;
        var density = kind == ParcelType.UltraThin ? .55m : kind == ParcelType.Liquid ? .25m : .09m + random.Next(0, 110) / 1000m;
        var weightKg = Math.Round(Math.Clamp(volume / 1000000m * density, .08m, 28m), 3);
        var outcome = pending ? 999 : random.Next(1000);
        var noRead = outcome < 15 ? (NoReadType)random.Next(2, 10) : NoReadType.None;
        var barcode = noRead != NoReadType.None ? "NoRead" : $"SIM-{kind.ToString().ToUpperInvariant()}-{day:yyyyMMdd}-{sequence:D4}";
        var exceptionCode = outcome switch { < 15 => "Simulation.NoRead", < 25 => "Simulation.ProviderRejected", < 35 => "ParcelSpacingViolation", < 45 => "TargetChuteAssignmentRejected", < 52 => "RoutingTimeout", < 60 => "SIM.UnmappedDeviceFault", _ => null };
        var chute = 6101L + random.Next(24);
        var carrier = station * 1000L + 1 + sourceNumber % 360;
        var provider = station < 2 ? "SimulatedExpressProvider" : "SimulatedWarehouseProvider";
        var taskCode = $"SIM-TASK-{day:yyyyMMdd}-{sequence:D4}";
        var image = sequence % 5 == 0 || exceptionCode is not null;
        var recovered = exceptionCode is null && !pending && sequence % 97 == 0;
        var retry = exceptionCode is null && !pending && sequence % 29 == 0;
        var records = new List<ParcelProcessingRecord>();
        var baseline = new ParcelProcessingRecordRequest {
            SourceInstanceId = source, SourceRunId = run, SourceParcelId = sourceNumber,
            WorkstationName = workstation, Barcode = barcode, TriggerBatch = $"{day:yyyyMMdd}-{station + 1:D2}", ScanSequence = sourceNumber.ToString("D6", CultureInfo.InvariantCulture),
            RawPayload = JsonSerializer.Serialize(new { simulation = true, source, sourceParcelId = sourceNumber })
        };
        void Add(ParcelProcessingRecordRequest request, ParcelProcessingStage stage, long milliseconds, int attempt = 1) {
            var occurred = time.AddMilliseconds(milliseconds);
            var recorded = occurred.AddMilliseconds(6);
            if (recorded > options.AsOf) throw new InvalidOperationException("模拟事实超出批次时钟。");
            request = request with { RecordId = $"sim-{day:yyyyMMdd}-{sequence:D4}-{(int)stage}-{attempt}", Stage = (int)stage, AttemptNumber = attempt, OccurredAt = occurred };
            var record = ParcelProcessingContractMapper.ToDomain(request, recorded) with {
                Key = IdentityHash(source, run, request.RecordId), ParcelId = id, PartitionTime = time.AddMilliseconds(6)
            };
            records.Add(record);
        }
        Add(baseline with { Barcode = null, PreviousCreationGapMilliseconds = gap, IsSuccess = true }, ParcelProcessingStage.Detected, 0);
        var measurement = baseline with {
            IsSuccess = true, WeightGrams = weightKg * 1000m, LengthMm = length, WidthMm = width, HeightMm = height, VolumeMm3 = volume,
            VolumetricWeightGrams = Math.Round(volume / 6000m, 3), MeasuredAt = time.AddMilliseconds(180), ReceivedAt = time.AddMilliseconds(220),
            HasReliableTimestamp = true, HasReliableFrameBoundary = true, CorrelationId = sourceNumber,
            MessageIdentity = $"SIM-DWS-{day:yyyyMMdd}-{station}-{sourceNumber:D6}", BarcodesJson = JsonSerializer.Serialize(new[] { barcode })
        };
        Add(measurement, ParcelProcessingStage.DwsReceived, 220);
        Add(measurement with { BindingMode = sequence % 17 == 0 ? "Fifo" : "Exact", CandidateSourceParcelId = sourceNumber, FinalSourceParcelId = sourceNumber, DeltaMilliseconds = 180, DecisionReason = "模拟：触发序号与DWS记录一致" }, ParcelProcessingStage.DwsBound, 320);
        var providerFailure = exceptionCode == "Simulation.ProviderRejected";
        var requestBody = JsonSerializer.Serialize(new { simulation = true, barcode, weightGrams = weightKg * 1000, dimensionsMm = new[] { length, width, height }, sourceParcelId = sourceNumber });
        var scan = baseline with {
            IsSuccess = !providerFailure && !retry, Provider = provider, TaskCode = providerFailure || retry ? null : taskCode,
            RequestUrl = $"https://provider.simulation.invalid/scan", RequestHeaders = "{\"X-Simulation\":\"true\"}", RequestBody = requestBody,
            ResponseBody = providerFailure ? "{\"success\":false,\"code\":\"ADDRESS_NOT_FOUND\",\"message\":\"模拟：目的地无法匹配\"}" : retry ? "{\"success\":false,\"code\":\"TEMPORARILY_UNAVAILABLE\"}" : JsonSerializer.Serialize(new { success = true, simulation = true, taskCode, chute }),
            ResponseStatusCode = providerFailure ? 422 : retry ? 503 : 200,
            ErrorMessage = providerFailure ? "模拟：外部接口拒绝路由" : retry ? "模拟：接口临时不可用，将重试" : null,
            RequestAt = time.AddMilliseconds(400), ResponseAt = time.AddMilliseconds(650), ElapsedMilliseconds = 250
        };
        if (noRead == NoReadType.None) Add(scan, ParcelProcessingStage.ScanUploaded, 650);
        if (retry) Add(scan with { IsSuccess = true, TaskCode = taskCode, ResponseStatusCode = 200, ErrorMessage = null, ResponseBody = JsonSerializer.Serialize(new { success = true, simulation = true, taskCode, chute }), RequestAt = time.AddMilliseconds(1450), ResponseAt = time.AddMilliseconds(1650), ElapsedMilliseconds = 200 }, ParcelProcessingStage.ScanUploaded, 1650, 2);
        if (!pending && noRead == NoReadType.None && !providerFailure && exceptionCode != "RoutingTimeout") {
            Add(baseline with { IsSuccess = exceptionCode != "TargetChuteAssignmentRejected", TargetChuteCode = chute.ToString(CultureInfo.InvariantCulture), TaskCode = taskCode, DecisionReason = exceptionCode == "TargetChuteAssignmentRejected" ? "模拟：设备拒绝目标格口" : "模拟：路由分配成功" }, ParcelProcessingStage.ChuteAssigned, 1900);
            if (exceptionCode != "TargetChuteAssignmentRejected") Add(baseline with { IsSuccess = true, DispatchedChuteCode = chute.ToString(CultureInfo.InvariantCulture) }, ParcelProcessingStage.SorterDispatched, 2000);
        }
        if (exceptionCode is not null || recovered) {
            var code = exceptionCode ?? "ParcelSpacingViolation";
            Add(baseline with { IsSuccess = false, ExceptionCode = code, IsRoutingBlocked = !recovered, IsSpacingViolation = code == "ParcelSpacingViolation", IsAwaitingWcsDecision = code == "RoutingTimeout", ErrorMessage = code switch {
                "Simulation.NoRead" => $"模拟：{noRead}，未获得有效条码",
                "Simulation.ProviderRejected" => "模拟：Provider响应ADDRESS_NOT_FOUND，转人工处理",
                "ParcelSpacingViolation" => recovered ? "模拟：调整间距后恢复分拣" : "模拟：安全间距不足，转异常处理",
                "TargetChuteAssignmentRejected" => "模拟：目标格口锁定，分配被拒绝",
                "RoutingTimeout" => "模拟：等待路由决策超时",
                _ => "模拟：未收录的设备错误编码，归类到未知异常"
            } }, ParcelProcessingStage.ParcelException, 3000);
        }
        if (!pending && exceptionCode is null) {
            var duration = random.Next(8200, 18201) + (retry ? 1000 : 0) + (recovered ? 3000 : 0);
            Add(baseline with { IsSuccess = true, ActualChuteCode = chute.ToString(CultureInfo.InvariantCulture), IsRoutingBlocked = false }, ParcelProcessingStage.SortingCompleted, duration);
            Add(baseline with { IsSuccess = true, Provider = provider, ActualChuteCode = chute.ToString(CultureInfo.InvariantCulture), RequestUrl = "https://provider.simulation.invalid/discharge", RequestBody = JsonSerializer.Serialize(new { simulation = true, barcode, actualChute = chute }), ResponseBody = "{\"success\":true,\"simulation\":true}", ResponseStatusCode = 200, RequestAt = time.AddMilliseconds(duration + 10), ResponseAt = time.AddMilliseconds(duration + 110), ElapsedMilliseconds = 100 }, ParcelProcessingStage.LandingReported, duration + 110);
        }
        var imageUrl = options.PublicBaseUrl.TrimEnd('/') + "/demo/parcel-sample.svg";
        if (image && !pending) {
            Add(baseline with { IsSuccess = true, ImagePath = imageUrl, ImageCamera = $"SIM-CAMERA-{station + 1:D2}" }, ParcelProcessingStage.ImageRegistered, 700);
            Add(baseline with { IsSuccess = true, ImagePath = imageUrl, ImageCamera = $"SIM-CAMERA-{station + 1:D2}" }, ParcelProcessingStage.ImageUploaded, 1200);
        }
        var parcel = Parcel.CreateDetected(id, records[0], records[0].RecordedAt);
        parcel.ApplyProcessingRecords(records, ClassificationRuleDefaults.Create());
        parcel.SetDeviceInfo(new() { MachineCode = source, WorkstationName = workstation, CustomName = $"模拟分拣机 {station + 1}" });
        parcel.SetSorterCarrierInfo(new() { SorterCarrierId = carrier, LoadedTime = time, ConveyorSpeedWhenLoaded = 1.8m + station * .1m, LinkedCarrierCount = kind == ParcelType.Large ? 2 : 1 });
        parcel.SetGrayDetectorInfo(new() { CarrierNumber = carrier.ToString(CultureInfo.InvariantCulture), LinkedCarrierCount = kind == ParcelType.Large ? 2 : 1, ResultTime = time.AddMilliseconds(80), RawResult = "SIMULATED: carrier occupied", CenterPosition = "800,320" });
        var sticking = exceptionCode == "ParcelSpacingViolation" && sequence % 3 == 0;
        parcel.SetStickingParcelInfo(new() { IsSticking = sticking, ReceiveTime = time.AddMilliseconds(100), ElapsedMilliseconds = 3, RawData = sticking ? "SIM: overlap detected" : "SIM: single parcel" });
        parcel.SetParcelPositionInfo(new() { X1 = 600, X2 = 600 + length, Y1 = 150, Y2 = 150 + width, BackgroundX1 = 0, BackgroundX2 = 1600, BackgroundY1 = 0, BackgroundY2 = 800 });
        parcel.AddBarCodeInfo(new() { BarCode = barcode, BarCodeType = BarCodeType.ExpressSheet, CapturedTime = measurement.MeasuredAt });
        if (noRead == NoReadType.None && sequence % 11 == 0) parcel.AddBarCodeInfo(new() { BarCode = $"SIM-PACK-{day:yyyyMMdd}-{sequence:D4}", BarCodeType = BarCodeType.ParcelMaterial, CapturedTime = measurement.MeasuredAt });
        parcel.AddWeightInfo(new() { FormattedWeight = weightKg, RawWeight = (weightKg * 1000).ToString(CultureInfo.InvariantCulture) + " g", EvidenceCode = measurement.MessageIdentity!, WeighingTime = measurement.MeasuredAt!.Value });
        parcel.SetVolumeInfo(new() { SourceType = VolumeSourceType.Camera3D, FormattedLength = length, FormattedWidth = width, FormattedHeight = height, FormattedVolume = volume, RawVolume = $"SIM:{length}x{width}x{height} mm", EvidenceCode = measurement.MessageIdentity!, MeasurementTime = measurement.MeasuredAt.Value, BindTime = time.AddMilliseconds(320) });
        if (parcel.Status == ParcelStatus.Completed) parcel.SetChuteInfo(new() { TargetChuteId = parcel.TargetChuteId, ActualChuteId = parcel.ActualChuteId, LandedTime = parcel.DischargeTime!.Value });
        foreach (var record in records.Where(r => r.RequestUrl is not null)) parcel.AddApiRequest(new() {
            ApiType = record.Stage == ParcelProcessingStage.LandingReported ? ApiRequestType.DischargeReport : ApiRequestType.ScanResult,
            RequestStatus = record.IsSuccess == true ? ApiRequestStatus.Success : ApiRequestStatus.Failed,
            RequestUrl = record.RequestUrl!, RequestTime = record.RequestAt!.Value, ResponseTime = record.ResponseAt, ElapsedMilliseconds = record.ElapsedMilliseconds!.Value,
            Headers = record.RequestHeaders ?? "", RequestBody = record.RequestBody!, ResponseBody = record.ResponseBody!, Exception = record.ErrorMessage ?? "", RawData = record.RawPayload!, FormattedMessage = $"模拟 {provider} 尝试 {record.AttemptNumber}"
        });
        foreach (var record in records.Where(r => r.Stage is ParcelProcessingStage.Detected or ParcelProcessingStage.SorterDispatched or ParcelProcessingStage.SortingCompleted or ParcelProcessingStage.ParcelException)) parcel.AddCommandInfo(new() {
            ProtocolType = ProtocolType.Tcp, ProtocolName = "Fusion模拟协议", ConnectionName = source, GeneratedTime = record.OccurredAt,
            Direction = record.Stage == ParcelProcessingStage.SorterDispatched ? CommandDirection.Send : CommandDirection.Receive,
            ActionType = record.Stage switch { ParcelProcessingStage.Detected => ActionType.CreateParcel, ParcelProcessingStage.ParcelException => ActionType.ParcelAbnormal, ParcelProcessingStage.SortingCompleted => ActionType.DischargeConfirmation, _ => ActionType.None },
            CommandPayload = JsonSerializer.Serialize(new { simulation = true, eventType = record.Stage.ToString(), sourceParcelId = sourceNumber, targetChute = record.DispatchedChuteCode, actualChute = record.ActualChuteCode, exceptionCode = record.ExceptionCode }), FormattedMessage = "模拟：" + record.Stage
        });
        if (image && !pending) parcel.AddImageInfo(new() { RelativePath = imageUrl, ImageType = ImageType.Panorama, CaptureType = ImageCaptureType.LocalMatched, CameraName = $"模拟相机 {station + 1}", CameraSerialNumber = $"SIM-CAMERA-{station + 1:D2}", CustomName = "模拟包裹图（非现场照片）", ContentType = "image/svg+xml", OriginalFileName = "parcel-sample.svg", UploadedAtLocal = time.AddMilliseconds(1200) });
        if (sequence % 7 == 0) parcel.AddVideoInfo(new() { Channel = station + 1, NvrSerialNumber = $"SIM-NVR-{station / 2 + 1:D2}", NodeType = VideoNodeType.Scan });
        return new(parcel, records.OrderBy(r => r.OccurredAt).ToArray(), kind, noRead, carrier, sticking);
    }

    /// <summary>造数前检查完整关联、量测单位、状态、事实与时间约束。</summary>
    public static void Validate(IReadOnlyCollection<SimulationParcel> data, SimulationOptions options) {
        if (data.Count != options.Count || data.Select(s => s.Parcel.Id).Distinct().Count() != options.Count)
            throw new InvalidOperationException("模拟总量或编号不一致。");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in data) {
            var p = sample.Parcel;
            if (p.CreatedTime < options.Start || p.CreatedTime > options.AsOf || p.MeasurementTime < p.DetectedTime || p.Weight <= 0 || p.Volume != p.Length * p.Width * p.Height || p.VolumeInfo?.FormattedVolume != p.Volume || p.WeightInfos[0].FormattedWeight != p.Weight)
                throw new InvalidOperationException($"包裹{idText(p)}量测或时间错误。");
            if ((p.Status == ParcelStatus.Completed) != p.CompletedTime.HasValue || p.CompletedTime > options.AsOf || p.CompletedTime < p.MeasurementTime || p.Status == ParcelStatus.Completed && (p.ActualChuteId is null || p.ExceptionType is not null))
                throw new InvalidOperationException($"包裹{idText(p)}终态与落格不一致。");
            foreach (var record in sample.Records) {
                record.Validate();
                if (!keys.Add(record.Key) || record.ParcelId != p.Id || record.SourceInstanceId != p.SourceInstanceId || record.SourceRunId != p.SourceRunId || record.SourceParcelId != p.SourceParcelId || record.RecordedAt < record.OccurredAt || record.RecordedAt > options.AsOf || record.PartitionTime != p.CreatedTime)
                    throw new InvalidOperationException("处理事实身份、分表锚点或时间错误。");
            }
        }
        /// <summary>为错误消息格式化模拟包裹编号。</summary>
        static string idText(Parcel p) => p.Id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>增补可编辑的草稿示例，既有已发布规则及系统兜底保持原文。</summary>
    public static ClassificationRule[] ExampleRules(bool exception, DateTime asOf) {
        var fields = exception ? new[] {
            new ClassificationCondition { Field = "包裹重量", Operator = "大于", Value = "20", Unit = "kg" },
            new ClassificationCondition { Field = "包裹体积（长×宽×高）", Operator = "大于", Value = "180000", Unit = "cm³" },
            new ClassificationCondition { Field = "条码", Operator = "等于", Value = "NoRead" },
            new ClassificationCondition { Field = "Provider 响应内容", Operator = "包含", Value = "ADDRESS_NOT_FOUND" }
        } : new[] {
            new ClassificationCondition { Field = "包裹重量", Operator = "大于或等于", Value = "10", Unit = "kg" },
            new ClassificationCondition { Field = "包裹高度", Operator = "小于或等于", Value = "25", Unit = "mm" },
            new ClassificationCondition { Field = "条码", Operator = "包含", Value = "SIM-FRAGILE" }
        };
        var names = exception ? new[] { "超重人工复核", "超大体积复核", "NoRead识读异常", "Provider目的地异常" } : new[] { "大件包裹分类", "超薄包裹分类", "易碎品分类" };
        return fields.Select((condition, i) => new ClassificationRule {
            Id = (exception ? 87000 : 86000) + i, Name = "模拟示例：" + names[i], Status = "草稿", Scope = "sim-fusion-01 / sim-fusion-02 / sim-fusion-03 / sim-fusion-04",
            Modified = asOf.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), Editor = "模拟数据工具", Description = "模拟数据的条件配置示例，确认后可自行发布。", Note = Marker,
            ExceptionType = exception ? i == 3 ? 1 : 15 : null, ParcelType = exception ? null : i == 0 ? 1 : i == 1 ? 3 : 6,
            Conditions = [condition], Actions = exception ? ["标记分拣异常", "记录异常原因"] : ["标记包裹类型"]
        }).ToArray();
    }
}
