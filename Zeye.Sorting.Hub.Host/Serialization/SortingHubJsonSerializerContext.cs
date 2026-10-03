using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Contracts.Models.Diagnostics;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

namespace Zeye.Sorting.Hub.Host.Serialization;

/// <summary>
/// 高频 API 合同的源生成 JSON 序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ParcelListResponse))]
[JsonSerializable(typeof(ParcelCursorListResponse))]
[JsonSerializable(typeof(ParcelDetailResponse))]
[JsonSerializable(typeof(ParcelImagesResponse))]
[JsonSerializable(typeof(ParcelAnalyticsResponse))]
[JsonSerializable(typeof(Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing.ParcelProcessingRecordRequest))]
[JsonSerializable(typeof(Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing.ParcelProcessingWriteResponse))]
[JsonSerializable(typeof(IReadOnlyList<Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing.ParcelProcessingRecordResponse>))]
[JsonSerializable(typeof(ParcelAdjacentResponse))]
[JsonSerializable(typeof(ParcelBatchBufferedCreateResponse))]
[JsonSerializable(typeof(WebRequestAuditLogListResponse))]
[JsonSerializable(typeof(WebRequestAuditLogDetailResponse))]
[JsonSerializable(typeof(SlowQueryProfileResponse))]
internal sealed partial class SortingHubJsonSerializerContext : JsonSerializerContext {
}
