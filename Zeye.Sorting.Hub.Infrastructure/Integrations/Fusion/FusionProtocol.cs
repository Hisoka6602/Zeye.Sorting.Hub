using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Application.Services.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>验证原始协议边界并映射既有处理用例，业务时间统一为登记来源的本地时间。</summary>
public static partial class FusionProtocol {
    /// <summary>独立设备接收端路径。</summary>
    public const string HubPath = "/hubs/fusion-ingestion";
    /// <summary>协议 JSON 配置只用于解析和完整消息限额，不参与原文摘要重写。</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 32 };
    /// <summary>所有支持的不可变事实类型。</summary>
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) {
        "parcel.detected", "parcel.measurement", "parcel.chute-assigned", "parcel.dispatched", "parcel.landed",
        "parcel.exception", "parcel.session-ended", "dws.received", "dws.binding", "provider.interaction",
        "provider.decision", "provider.operation", "image.captured", "image.association", "image.provider-upload",
        "image.stored", "source.counter-run", "source.identity-conflict"
    };
    /// <summary>稳定来源编码的允许字符。</summary>
    [GeneratedRegex("^[A-Za-z0-9._-]{1,96}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentityPattern();
    /// <summary>稳定编码验证。</summary>
    public static bool IsIdentity(string? value) => value is not null && IdentityPattern().IsMatch(value);
    /// <summary>小写固定长度摘要验证。</summary>
    public static bool IsHex(string? value, int length) => value?.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    /// <summary>不可变原始 UTF-8 内容摘要。</summary>
    public static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    /// <summary>不可变二进制内容摘要。</summary>
    public static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    /// <summary>长度前缀通过 JSON 数组表达，防止身份分隔符产生碰撞。</summary>
    public static string Key(params string[] parts) => Hash(JsonSerializer.Serialize(parts));
    /// <summary>读取有效 Int64 十进制序号，禁止截断到双精度数字。</summary>
    public static long Number(string? value, bool canonical = false) {
        if (string.IsNullOrEmpty(value) || value.Any(c => c is < '0' or > '9')
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
            || canonical && value != number.ToString(CultureInfo.InvariantCulture)) throw new ArgumentException("InvalidInt64");
        return number;
    }
    /// <summary>先校验信封原文与编号，避免不可变事实的内容变更被误判为新报文格式错误。</summary>
    public static long ValidateEnvelope(HubFactEnvelope envelope) {
        if (!IsHex(envelope.RecordId, 32) || !IsHex(envelope.BodySha256, 64)
            || string.IsNullOrEmpty(envelope.BodyJson) || envelope.BodySha256 != Hash(envelope.BodyJson)) throw new ArgumentException("InvalidEnvelope");
        return Number(envelope.SourceSequence, true);
    }
    /// <summary>验证来源事实的外内层身份和摘要；未知字段通过原文完整保留。</summary>
    public static HubFactBody Decode(HubFactEnvelope envelope, string source, string journal, FusionSourceOptions registered) {
        ValidateEnvelope(envelope);
        // 步骤1：先验证原文 SHA，再解析；不根据解析结果重新计算摘要。
        using var document = JsonDocument.Parse(envelope.BodyJson, new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        var required = new[] { "schemaVersion", "sourceInstanceId", "journalId", "sourceRunId", "sourceParcelId",
            "recordId", "sourceSequence", "kind", "occurredAtUtc", "capturedAtUtc", "timeZoneId",
            "producerSessionId", "producerVersion", "configurationRevision", "data" };
        if (root.ValueKind != JsonValueKind.Object || required.Any(p => !root.TryGetProperty(p, out _))
            || root.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
            throw new ArgumentException("InvalidFactBody");
        var fact = JsonSerializer.Deserialize<HubFactBody>(envelope.BodyJson, Json) ?? throw new ArgumentException("InvalidFactBody");
        if (fact.SchemaVersion != "1.0" || fact.SourceInstanceId != source || fact.JournalId != journal
            || fact.RecordId != envelope.RecordId || fact.SourceSequence != envelope.SourceSequence
            || !IsHex(fact.SourceRunId, 32) || !IsHex(fact.ProducerSessionId, 32) || !IsHex(fact.ConfigurationRevision, 64)
            || string.IsNullOrEmpty(fact.ProducerVersion) || fact.ProducerVersion.Length > 256
            || fact.TimeZoneId != registered.TimeZoneId || fact.Data.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("FactIdentityMismatch");
        if (!Kinds.Contains(fact.Kind)) throw new ArgumentException("UnsupportedFactKind");
        // 步骤2：协议携带绝对时间，入库用例只接收来源本地时间。
        Local(fact.OccurredAtUtc, registered.TimeZoneId); Local(fact.CapturedAtUtc, registered.TimeZoneId);
        if (fact.SourceParcelId is not null) Number(fact.SourceParcelId);
        if (fact.Kind is "parcel.detected" or "parcel.measurement" or "parcel.chute-assigned" or "parcel.dispatched" or "parcel.landed"
            && fact.SourceParcelId is null) throw new ArgumentException("MissingParcelIdentity");
        if (fact.Kind == "dws.received" && fact.SourceParcelId is not null) throw new ArgumentException("DwsMustRemainUnbound");
        return fact;
    }
    /// <summary>协议绝对时间转换只发生在边界，业务模型不包含偏移或通用时间语义。</summary>
    public static DateTime Local(DateTimeOffset value, string zone) {
        if (value == default || value.Offset != TimeSpan.Zero) throw new ArgumentException("InvalidProtocolTimestamp");
        return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime;
    }
    /// <summary>映射当前事实的真实阶段；快照中的历史状态不会制造额外业务事件。</summary>
    public static ParcelProcessingRecordRequest? Map(HubFactBody fact, FusionSourceOptions registered) {
        var data = fact.Data;
        var stage = fact.Kind switch {
            "parcel.detected" => 0, "dws.received" => 1, "dws.binding" or "parcel.measurement" => 2,
            "provider.interaction" => 3, "provider.decision" or "parcel.chute-assigned" => 4,
            "parcel.dispatched" => 5, "parcel.landed" => 6, "parcel.exception" => 7,
            "provider.operation" => Text(data, "operation") == "landing" ? 8 : 3,
            "image.captured" or "image.association" => 9, "image.provider-upload" => 10, _ => -1
        };
        if (stage < 0) return null;
        var association = fact.Kind == "image.association" && Boolean(data, "associationConfirmed") == true
            && (Decimal(data, "candidateCount") ?? 1) <= 1;
        var sourceParcel = fact.SourceParcelId is null ? (long?)null : Number(fact.SourceParcelId);
        // 未绑定的非 DWS 事实仍保留原文，但不能为既有聚合制造假包裹。
        if (stage == 9 && !association || sourceParcel is null && stage is not (1 or 2)) return null;
        var success = fact.Kind switch {
            "parcel.measurement" => true,
            "dws.binding" => Text(data, "decision") is "Committed" or "PartialBound" && Long(data, "finalParcelId") is not null,
            "provider.decision" => Boolean(data, "isAssigned"),
            "parcel.chute-assigned" => !string.IsNullOrWhiteSpace(Text(data, "targetChute")),
            "parcel.dispatched" => Boolean(data, "isSorterDispatched"),
            "parcel.landed" => Boolean(data, "actualLandingConfirmed") == true ? true : throw new ArgumentException("LandingNotConfirmed"),
            "image.association" => association,
            "image.provider-upload" => Boolean(data, "accepted"),
            "provider.operation" => Boolean(data, "businessAccepted"),
            _ => (bool?)null
        };
        if (fact.Kind == "dws.binding" && success != true) sourceParcel = null;
        if (fact.Kind == "parcel.detected" && Boolean(data, "hasSorterDetection") == false) throw new ArgumentException("DetectionNotConfirmed");
        var occurred = Local(fact.OccurredAtUtc, registered.TimeZoneId);
        if (stage == 0 && ProtocolTime(data, "detectedAtUtc", registered.TimeZoneId) is { } detected) occurred = detected;
        if (stage == 6 && ProtocolTime(data, "landedAtUtc", registered.TimeZoneId) is { } landed) occurred = landed;
        var imageId = Text(data, "sourceImageId", 128);
        if (stage is 9 or 10 && string.IsNullOrWhiteSpace(imageId)) throw new ArgumentException("MissingImageIdentity");
        var length = Decimal(data, "lengthMm"); var width = Decimal(data, "widthMm"); var height = Decimal(data, "heightMm");
        var request = new ParcelProcessingRecordRequest {
            RecordId = Key(fact.SourceInstanceId, fact.JournalId, fact.RecordId), SourceInstanceId = fact.SourceInstanceId,
            SourceRunId = fact.SourceRunId, SourceParcelId = sourceParcel, Stage = stage, OccurredAt = occurred,
            WorkstationName = string.IsNullOrEmpty(registered.WorkstationName) ? registered.SourceInstanceId : registered.WorkstationName,
            IsSuccess = success, AttemptNumber = (int)Math.Clamp(Decimal(data, "attemptNumber") ?? 1, 1, int.MaxValue),
            Barcode = Text(data, "barcode", 1024), WeightGrams = Decimal(data, "weightGrams"),
            LengthMm = length, WidthMm = width, HeightMm = height,
            VolumeMm3 = length > 0 && width > 0 && height > 0 ? checked(length * width * height) : null,
            VolumetricWeightGrams = Decimal(data, "volumetricWeightGrams"),
            PreviousCreationGapMilliseconds = (long?)Decimal(data, "previousCreationGapMilliseconds"),
            IsSpacingViolation = Boolean(data, "isSpacingViolation"), IsAwaitingWcsDecision = Boolean(data, "isAwaitingWcsDecision"),
            MeasuredAt = ProtocolTime(data, "measuredAtUtc", registered.TimeZoneId),
            ReceivedAt = ProtocolTime(data, "receivedAtUtc", registered.TimeZoneId),
            HasReliableTimestamp = Boolean(data, "hasReliableDetectionTimestamp"),
            Provider = Text(data, "provider", 96), TaskCode = Text(data, "taskCode", 256),
            TargetChuteCode = Text(data, "targetChute", 128) ?? Text(data, "chuteCode", 128),
            DispatchedChuteCode = stage == 5 ? Text(data, "targetChute", 128) : null,
            ActualChuteCode = stage == 6 ? Text(data, "actualChute", 128) : null,
            IsFallback = Boolean(data, "isFallbackChuteAssigned"), IsRoutingBlocked = Boolean(data, "isRoutingBlocked"),
            BindingMode = Text(data, "correlationMode", 32) ?? Text(data, "fusionMode", 32),
            CandidateSourceParcelId = Long(data, "candidateParcelId"), FinalSourceParcelId = fact.Kind == "parcel.measurement" ? sourceParcel : Long(data, "finalParcelId"),
            CorrelationId = Long(data, "correlationId") ?? Long(data, "correlationIdHint"),
            TriggerBatch = Text(data, "triggerBatch", 128), ScanSequence = Text(data, "scanSequence", 128),
            MessageIdentity = Text(data, "messageIdentity", 256), DeltaMilliseconds = Decimal(data, "deltaMilliseconds"),
            DecisionReason = Text(data, "reason", 2048),
            ExceptionCode = stage == 7 ? Text(data, "exceptionType", 128) ?? (Boolean(data, "isDwsTimedOut") == true ? "DwsTimeout"
                : Boolean(data, "isSpacingViolation") == true ? "ParcelSpacingViolation" : Text(data, "stage", 128) ?? "Unknown") : null,
            ErrorMessage = Text(data, "message", 2048) ?? Text(data, "detail", 2048),
            RawPayload = fact.Data.GetRawText(), RequestUrl = Text(data, "url", 512),
            RequestBody = Text(data, "request", 8192), ResponseBody = Text(data, "response", 8192),
            ResponseStatusCode = (int?)Decimal(data, "statusCode"), ElapsedMilliseconds = (int?)Decimal(data, "durationMs"),
            ImagePath = imageId is null ? null : "/api/parcels/fusion/images/" + Key(fact.SourceInstanceId, imageId) + "/content",
            ImageCamera = Text(data, "cameraName", 128), ImageContentHash = Text(data, "contentSha256", 64)
        };
        // 步骤3：入库确认前执行既有领域验证，拒绝无法投影的输入而非永久挂起任务。
        ParcelProcessingContractMapper.ToDomain(request, DateTime.Now).Validate();
        return request;
    }
    /// <summary>读取有界字符串；完整截断前内容仍保留在原始接收簿。</summary>
    public static string? Text(JsonElement data, string name, int limit = 256) => data.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() is { } text ? text[..Math.Min(text.Length, limit)] : null;
    /// <summary>读取可空布尔值，不推断未知业务结果。</summary>
    public static bool? Boolean(JsonElement data, string name) => data.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    /// <summary>读取可空有限精度量测。</summary>
    public static decimal? Decimal(JsonElement data, string name) => data.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : null;
    /// <summary>读取字符串形式的设备关联编号。</summary>
    private static long? Long(JsonElement data, string name) => Text(data, name) is { } value
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 ? number : null;
    /// <summary>读取可空来源绝对事件时间并转换为来源本地时间。</summary>
    private static DateTime? ProtocolTime(JsonElement data, string name, string zone) => Text(data, name) is { } value
        ? Local(DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None), zone) : null;
}
