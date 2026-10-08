using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>用明确来源包裹关联检测和有效条码接收，未知时间不替换为设备量测、绑定或Hub入库时间。</summary>
internal static class ParcelDwsScanTimingReader {
    /// <summary>从相同测量身份的真实接收事实取结束时间；仅有绑定条码不能证明扫码结束时间。</summary>
    internal static DwsMeasurementSample ReceiveEndpoint(DwsMeasurementSample sample, ParcelDwsMeasurementSnapshot[] facts) {
        if (facts.Where(row => row.SourceParcelId.HasValue).Select(row => row.SourceParcelId).Distinct().Take(2).Count() > 1)
            return sample with { ScanTimingUnavailableReason = "conflicting-parcel-identity" };
        var received = facts.Where(row => row.Stage == ParcelProcessingStage.DwsReceived && row.Barcode?.Trim() == sample.Barcode).ToArray();
        if (received.Length == 0) return sample with { ScanTimingUnavailableReason = "missing-scan-result" };
        if (received.Any(row => row.HasReliableTimestamp == false)) return sample with { ScanTimingUnavailableReason = "unreliable-scan-time" };
        var endpoints = received.Select(row => row.ReceivedAt ?? row.OccurredAt).Distinct().ToArray();
        if (endpoints.Length != 1) return sample with { ScanTimingUnavailableReason = "conflicting-scan-time" };
        var end = endpoints[0];
        return ValidTime(end) ? sample with { ScanCompletedAt = end,
            ScanTimingBasis = received.Any(row => row.ReceivedAt.HasValue) ? "source-received" : "source-event" }
            : sample with { ScanTimingUnavailableReason = "invalid-scan-time" };
    }

    /// <summary>分表内单次流式读取窄检测字段，少量编号走IN筛选，完整来源身份必须同时匹配。</summary>
    internal static async Task<List<DwsMeasurementSample>> ReadAsync(List<DwsMeasurementSample> samples, SortingHubDbContext db,
        ParcelPartitionCatalogSnapshot catalog, string[] suffixes, ReportingQueryBudget budget, string? sourceInstanceId, CancellationToken token, IReadOnlySet<string>? indexed = null) {
        // 大整数身份只解析一次，筛选与读取后端点校验共用同一结果。
        var identified = samples.Select(sample => (Sample: sample, Identity: Identity(sample))).ToArray();
        var wanted = identified.Where(item => item.Sample.ScanCompletedAt.HasValue && item.Identity.HasValue)
            .Select(item => item.Identity!.Value).ToHashSet();
        var detections = new Dictionary<(string Instance, string Run, long SourceParcel, long Parcel), List<ParcelDwsDetectionSnapshot>>();
        if (wanted.Count > 0) {
            var ids = wanted.Select(identity => identity.Parcel).Distinct().ToArray();
            await using var read = ParcelPartitionReadContext<ParcelDwsDetectionSnapshot>.Create<ParcelProcessingRecord>(db, catalog.Suffixes, streaming: true);
            await using var measurements = ParcelPartitionReadContext<ParcelDwsDetectionSnapshot>.Create<ParcelDwsMeasurementSnapshot>(db, catalog.Suffixes, streaming: true);
            var count = 0;
            foreach (var suffix in suffixes) {
                var source = indexed?.Contains(suffix) == true ? measurements : read;
                var query = source.Query([suffix], nameof(ParcelProcessingRecord.PartitionTime), budget.RangeStartLocal, budget.RangeEndLocal, false)
                    .Where(row => row.Stage == ParcelProcessingStage.Detected && row.ParcelId != null && row.SourceParcelId != null && row.IsSuccess != false);
                if (ids.Length <= 500) query = query.Where(row => ids.Contains(row.ParcelId!.Value));
                if (sourceInstanceId is not null) query = query.Where(row => row.SourceInstanceId == sourceInstanceId);
                await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(query, budget.RangeStartLocal, budget.RangeEndLocal, token,
                    targetRows: ParcelAnalysisWindowReader.TargetRows * 2))
                await foreach (var row in window.AsAsyncEnumerable().WithCancellation(token)) {
                    var identity = (row.SourceInstanceId, row.SourceRunId, row.SourceParcelId!.Value, row.ParcelId!.Value);
                    if (!wanted.Contains(identity)) continue;
                    if (count++ == 200000) throw new ArgumentException("扫码检测记录超过20万条，请缩小日期或来源范围。");
                    if (!detections.TryGetValue(identity, out var records)) detections[identity] = records = [];
                    records.Add(row);
                }
            }
        }
        return identified.Select(item => Complete(item.Sample, item.Identity, detections)).ToList();
    }

    /// <summary>重复事实先去重，矛盾的检测时间、明确不可靠时间或倒序时间不制造有效耗时。</summary>
    private static DwsMeasurementSample Complete(DwsMeasurementSample sample, (string Instance, string Run, long SourceParcel, long Parcel)? identity,
        Dictionary<(string Instance, string Run, long SourceParcel, long Parcel), List<ParcelDwsDetectionSnapshot>> detections) {
        if (sample.ScanTimingUnavailableReason is not null) return sample;
        if (identity is null) return sample with { ScanTimingUnavailableReason = "missing-parcel-identity" };
        if (!detections.TryGetValue(identity.Value, out var rows)) return sample with { ScanTimingUnavailableReason = "missing-detection" };
        var facts = rows.DistinctBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var times = facts.Select(row => row.OccurredAt).Distinct().ToArray();
        if (times.Length != 1) return sample with { ScanTimingUnavailableReason = "conflicting-detection-time" };
        var start = times[0]; sample = sample with { ScanStartedAt = start };
        if (!ValidTime(start) || facts.Any(row => row.HasReliableTimestamp == false)) return sample with { ScanTimingUnavailableReason = "unreliable-detection-time" };
        if (sample.ScanCompletedAt is not { } end) return sample with { ScanTimingUnavailableReason = "missing-scan-result" };
        return end < start ? sample with { ScanTimingUnavailableReason = "reversed-scan-time" }
            : sample with { ScanDurationMilliseconds = (end - start).Ticks / (decimal)TimeSpan.TicksPerMillisecond };
    }

    /// <summary>不按条码猜测包裹，来源实例、会话、设备号和Hub编号必须完整明确。</summary>
    private static (string Instance, string Run, long SourceParcel, long Parcel)? Identity(DwsMeasurementSample sample) =>
        long.TryParse(sample.SourceParcelId, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceParcel) && sourceParcel > 0
        && long.TryParse(sample.ParcelId, NumberStyles.None, CultureInfo.InvariantCulture, out var parcel) && parcel > 0
            ? (sample.SourceInstanceId, sample.SourceRunId, sourceParcel, parcel) : null;

    /// <summary>拒绝缺省日期和不同时间语义，避免时区换算伪造扫码耗时。</summary>
    private static bool ValidTime(DateTime time) => time != default && time.Kind is DateTimeKind.Local or DateTimeKind.Unspecified;
}
