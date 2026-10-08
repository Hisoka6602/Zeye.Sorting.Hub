using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>读取完整补齐的DWS窄表或原事实标量投影，保持量测及扫码统计口径。</summary>
public sealed class ParcelDwsConsistencyReadService(IDbContextFactory<SortingHubDbContext> factory,
    ReportingQueryBudgetPlanner planner, ParcelPartitionStore partitions, ParcelDwsConsistencyCache? cache = null) : IParcelDwsConsistencyReadService {
    /// <summary>防止过宽时间范围造成无界报表内存；超限明确拒绝而非返回不完整统计。</summary>
    private const int MaximumRecords = 200000;

    /// <summary>阈值、排行分页和所选条码复用相同量测快照，明细筛选不改变总体。</summary>
    public async Task<ParcelDwsConsistencyResponse> ReadAsync(ParcelDwsConsistencyRequest request, CancellationToken cancellationToken) {
        request = Validate(request);
        ReportingQueryBudget budget;
        try { budget = planner.BuildBudget(request.FromDate, request.ToDate.AddDays(1), null, false); }
        catch (InvalidOperationException exception) { throw new ArgumentException(exception.Message, nameof(request), exception); }
        var catalog = await partitions.GetReadCatalogAsync(cancellationToken);
        var suffixes = catalog.Periods.Where(period => period.Start < budget.RangeEndLocal && period.End > budget.RangeStartLocal)
            .Select(period => period.Suffix).Append(string.Empty).ToArray();
        var key = JsonSerializer.Serialize(new { request.FromDate, request.ToDate, request.SourceInstanceId, request.WorkstationName, Suffixes = suffixes });
        Task<ParcelDwsMeasurementDataset> Load() => LoadAsync(request, catalog, suffixes, budget, cancellationToken);
        var data = cache is null ? await Load() : await cache.GetAsync(key, request.Refresh, Load, cancellationToken);
        var selected = data.Samples.Where(sample => request.Barcode is null || sample.Barcode == request.Barcode).ToArray();
        var grouped = selected.GroupBy(sample => sample.Barcode, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        // 单票条码没有重复性或跨来源偏差；仅在显式参考值或详情需要时构造其完整指标。
        // 总测量数、来源计数和扫码有效数仍使用全部样本，不缩减统计总体。
        var summaries = grouped.Where(group => group.Value.Length >= 2 || request.ReferenceWeightGrams.HasValue
                || request.ReferenceVolumeCm3.HasValue || group.Key == request.DetailBarcode)
            .ToDictionary(group => group.Key, group => Summarize(group.Value, request), StringComparer.Ordinal);
        var repeated = summaries.Values.Where(group => group.MeasurementCount >= 2).ToArray();
        var candidates = summaries.Values.Where(group => group.MeasurementCount >= 2 || request.ReferenceWeightGrams.HasValue || request.ReferenceVolumeCm3.HasValue);
        if (request.OnlyDeviations) candidates = candidates.Where(group => group.WeightDeviates || group.VolumeDeviates || group.ScanDurationDeviates || group.ReferenceDeviates);
        var ordered = (request.SortBy == "volume" ? candidates.OrderByDescending(group => group.Volume.SpreadPercent)
            : request.SortBy == "scan-duration" ? candidates.OrderByDescending(group => group.ScanDuration.SpreadPercent)
            : request.SortBy == "scan-duration-p95" ? candidates.OrderByDescending(group => group.ScanDuration.P95)
            : request.SortBy == "count" ? candidates.OrderByDescending(group => (decimal?)group.MeasurementCount)
            : candidates.OrderByDescending(group => group.Weight.SpreadPercent)).ThenBy(group => group.Barcode, StringComparer.Ordinal).ToArray();
        var sources = Sources(selected, request, summaries);
        DwsConsistencyDetail? detail = null;
        if (request.DetailBarcode is not null && grouped.TryGetValue(request.DetailBarcode, out var detailSamples)) {
            var chronological = detailSamples.Where(sample => sample.MeasuredAt.HasValue).OrderBy(sample => sample.MeasuredAt).ThenBy(sample => sample.Key, StringComparer.Ordinal).ToArray();
            var trend = chronological.Length <= 200 ? chronological : Enumerable.Range(0, 200).Select(index => chronological[(int)((long)index * (chronological.Length - 1) / 199)]).ToArray();
            var scans = detailSamples.Where(sample => sample.ScanDurationMilliseconds.HasValue && sample.ScanCompletedAt.HasValue)
                .OrderBy(sample => sample.ScanCompletedAt).ThenBy(sample => sample.Key, StringComparer.Ordinal).ToArray();
            var scanTrend = scans.Length <= 200 ? scans : Enumerable.Range(0, 200).Select(index => scans[(int)((long)index * (scans.Length - 1) / 199)]).ToArray();
            detail = new() { Summary = summaries[request.DetailBarcode], MeasurementCount = detailSamples.Length,
                Items = detailSamples.OrderByDescending(sample => sample.MeasuredAt ?? sample.OccurredAt).ThenBy(sample => sample.Key, StringComparer.Ordinal)
                    .Skip((request.MeasurementPageNumber - 1) * 20).Take(20).ToArray(), Trend = trend,
                TrendTruncated = chronological.Length > 200, ScanTrend = scanTrend, ScanTrendTruncated = scans.Length > 200,
                Sources = Sources(detailSamples, request, summaries).Take(100).ToArray() };
        }
        return new() { GeneratedAt = data.GeneratedAt, MeasurementCount = selected.Length, RepeatedBarcodeCount = repeated.Length,
            ComparableBarcodeCount = repeated.Count(group => group.IsComparable), DeviationBarcodeCount = repeated.Count(group => group.WeightDeviates || group.VolumeDeviates || group.ScanDurationDeviates),
            ScanTimingSampleCount = selected.Count(sample => sample.ScanDurationMilliseconds.HasValue), MissingScanTimingCount = selected.Count(sample => !sample.ScanDurationMilliseconds.HasValue),
            MissingIdentityCount = data.MissingIdentityCount, ConflictingMeasurementCount = data.ConflictingMeasurementCount, MissingBarcodeCount = data.MissingBarcodeCount,
            DuplicateRecordCount = data.DuplicateRecordCount, FilteredCount = ordered.Length, Items = ordered.Skip((request.PageNumber - 1) * 20).Take(20).ToArray(),
            Sources = sources.Take(100).ToArray(), SourcesTruncated = sources.Length > 100, Detail = detail };
    }

    /// <summary>只读DWS接收及成功绑定窄列，晚到的绑定仍按固定入库分表归组。</summary>
    private async Task<ParcelDwsMeasurementDataset> LoadAsync(ParcelDwsConsistencyRequest request, ParcelPartitionCatalogSnapshot catalog,
        string[] suffixes, ReportingQueryBudget budget, CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        await using var read = ParcelPartitionReadContext<ParcelDwsMeasurementSnapshot>.Create<ParcelProcessingRecord>(db, catalog.Suffixes, streaming: true);
        await using var measurements = ParcelPartitionReadContext<ParcelDwsMeasurementSnapshot>.Create<ParcelDwsMeasurementSnapshot>(db, catalog.Suffixes, streaming: true);
        var indexed = (await db.Set<ParcelDurationBackfillState>().AsNoTracking().Where(row => row.DwsCompleted && suffixes.Contains(row.Suffix))
            .Select(row => row.Suffix).ToListAsync(token)).ToHashSet(StringComparer.Ordinal);
        var rows = new List<ParcelDwsMeasurementSnapshot>();
        foreach (var suffix in suffixes) {
            // 完整补齐的分表读取耐久窄表；未完成的历史保持原有统计口径。
            var source = indexed.Contains(suffix) ? measurements : read;
            var query = source.Query([suffix], nameof(ParcelProcessingRecord.PartitionTime), budget.RangeStartLocal, budget.RangeEndLocal, false)
                .Where(row => (row.Stage == ParcelProcessingStage.DwsReceived || row.Stage == ParcelProcessingStage.DwsBound) && row.IsSuccess != false)
                .Where(row => row.Stage != ParcelProcessingStage.DwsBound || row.IsSuccess == true);
            if (request.SourceInstanceId is not null) query = query.Where(row => row.SourceInstanceId == request.SourceInstanceId);
            if (request.WorkstationName is not null) query = query.Where(row => row.WorkstationName == request.WorkstationName);
            // DWS 窄量测采用较大窗口，避免中小总体为同一索引反复打开读取器；仍不截断统计总体。
            await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(query, budget.RangeStartLocal, budget.RangeEndLocal, token,
                targetRows: ParcelAnalysisWindowReader.TargetRows * 2))
            await foreach (var row in window.AsAsyncEnumerable().WithCancellation(token)) {
                // 提供器排序规则可能不区分大小写；业务来源仍执行精确匹配。
                if (request.SourceInstanceId is not null && row.SourceInstanceId != request.SourceInstanceId
                    || request.WorkstationName is not null && row.WorkstationName != request.WorkstationName) continue;
                if (rows.Count == MaximumRecords) throw new ArgumentException("量测记录超过20万条，请缩小日期或来源范围。");
                rows.Add(row);
            }
        }
        // 只在缺少测量条码时读取窄包裹身份，不加载Owned集合，也不按相同条码猜测关联包裹。
        var parcelIdentities = new Dictionary<long, ParcelDwsParcelIdentitySnapshot>();
        // 完整条码总体无需再次创建全部身份分组；存在缺失时仍按完整组判断。
        var missingIds = rows.Any(row => string.IsNullOrWhiteSpace(row.Barcode)) ? rows.Where(HasIdentity).GroupBy(row => (row.SourceInstanceId, row.SourceRunId, row.MessageIdentity))
            .Where(group => group.All(row => string.IsNullOrWhiteSpace(row.Barcode)))
            .SelectMany(group => group.Where(row => row.ParcelId.HasValue).Select(row => row.ParcelId!.Value)).Distinct().ToArray() : [];
        if (missingIds.Length > 0) {
            await using var parcelRead = ParcelPartitionReadContext<ParcelDwsParcelIdentitySnapshot>.Create<Parcel>(db, catalog.Suffixes, streaming: true);
            var cohort = parcelRead.Query(suffixes, nameof(Parcel.CreatedTime), budget.RangeStartLocal, budget.RangeEndLocal, false);
            if (request.SourceInstanceId is not null) cohort = cohort.Where(parcel => parcel.SourceInstanceId == request.SourceInstanceId);
            if (request.WorkstationName is not null) cohort = cohort.Where(parcel => parcel.WorkstationName == request.WorkstationName);
            // Oracle的IN参数有上限，所有提供器使用500编号的有界批次。
            foreach (var ids in missingIds.Chunk(500)) {
                var identities = await cohort.Where(parcel => ids.Contains(parcel.Id)).ToArrayAsync(token);
                foreach (var parcel in identities) parcelIdentities.TryAdd(parcel.Id, parcel);
            }
        }
        var distinct = rows.DistinctBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var duplicates = rows.Count - distinct.Length;
        var missingIdentity = distinct.Count(row => !HasIdentity(row));
        var conflicts = 0; var missingBarcode = 0;
        var samples = new List<DwsMeasurementSample>();
        foreach (var group in distinct.Where(HasIdentity)
            .GroupBy(row => (row.SourceInstanceId, row.SourceRunId, row.MessageIdentity))) {
            var facts = group.OrderBy(row => row.Stage == ParcelProcessingStage.DwsReceived ? 0 : 1).ThenBy(row => row.OccurredAt).ThenBy(row => row.Key, StringComparer.Ordinal).ToArray();
            duplicates += facts.Length - 1;
            var barcodes = facts.Select(row => Barcode(row, parcelIdentities)).Where(value => value is not null).Distinct(StringComparer.Ordinal).ToArray();
            if (barcodes.Length > 1 || Conflicts(facts.Select(row => row.WeightGrams)) || Conflicts(facts.Select(row => row.LengthMm))
                || Conflicts(facts.Select(row => row.WidthMm)) || Conflicts(facts.Select(row => row.HeightMm)) || Conflicts(facts.Select(ParcelDwsConsistencyMath.Volume))
                || facts.Where(row => row.ParcelId.HasValue).Select(row => row.ParcelId).Distinct().Take(2).Count() > 1
                || facts.Where(row => row.MeasuredAt.HasValue).Select(row => row.MeasuredAt).Distinct().Take(2).Count() > 1) { conflicts++; continue; }
            if (barcodes.Length == 0) { missingBarcode++; continue; }
            var primary = facts[0];
            var binding = facts.FirstOrDefault(row => row.Stage == ParcelProcessingStage.DwsBound && row.IsSuccess == true && row.ParcelId.HasValue);
            var sample = new DwsMeasurementSample { Key = JsonSerializer.Serialize(new[] { primary.SourceInstanceId, primary.SourceRunId, primary.MessageIdentity }), MessageIdentity = primary.MessageIdentity!, RecordId = primary.RecordId,
                Barcode = barcodes[0]!, SourceInstanceId = primary.SourceInstanceId, SourceRunId = primary.SourceRunId,
                SourceParcelId = (binding?.SourceParcelId ?? primary.SourceParcelId)?.ToString(CultureInfo.InvariantCulture), WorkstationName = binding?.WorkstationName ?? primary.WorkstationName,
                ParcelId = (binding?.ParcelId ?? primary.ParcelId)?.ToString(CultureInfo.InvariantCulture), BindingConfirmed = binding is not null,
                OccurredAt = primary.OccurredAt, MeasuredAt = facts.Select(row => row.MeasuredAt).FirstOrDefault(value => value.HasValue),
                WeightGrams = facts.Select(row => row.WeightGrams).FirstOrDefault(value => value is >= 0),
                LengthMm = facts.Select(row => row.LengthMm).FirstOrDefault(value => value is > 0), WidthMm = facts.Select(row => row.WidthMm).FirstOrDefault(value => value is > 0),
                HeightMm = facts.Select(row => row.HeightMm).FirstOrDefault(value => value is > 0), VolumeCm3 = facts.Select(ParcelDwsConsistencyMath.Volume).FirstOrDefault(value => value.HasValue) };
            samples.Add(ParcelDwsScanTimingReader.ReceiveEndpoint(sample, facts));
        }
        samples = await ParcelDwsScanTimingReader.ReadAsync(samples, db, catalog, suffixes, budget, request.SourceInstanceId, token, indexed);
        return new(DateTime.Now, samples, missingIdentity, conflicts, missingBarcode, duplicates);
    }

    /// <summary>不同非空值不能拼成一次可靠量测；同身份冲突整组排除。</summary>
    private static bool Conflicts(IEnumerable<decimal?> values) {
        decimal? first = null;
        foreach (var value in values) if (value.HasValue) {
            if (first.HasValue && first.Value != value.Value) return true;
            first = value;
        }
        return false;
    }

    /// <summary>测量身份同时依赖来源、运行会话和消息标识，不以缺失来源猜测设备。</summary>
    private static bool HasIdentity(ParcelDwsMeasurementSnapshot row) => !string.IsNullOrWhiteSpace(row.SourceInstanceId)
        && !string.IsNullOrWhiteSpace(row.SourceRunId) && !string.IsNullOrWhiteSpace(row.MessageIdentity);

    /// <summary>仅通过完整来源身份复用包裹条码，NoRead和空条码不进入一致性分组。</summary>
    private static string? Barcode(ParcelDwsMeasurementSnapshot row, IReadOnlyDictionary<long, ParcelDwsParcelIdentitySnapshot> parcels) {
        var value = row.Barcode;
        if (string.IsNullOrWhiteSpace(value) && row.ParcelId is { } id && parcels.TryGetValue(id, out var parcel)
            && parcel.SourceInstanceId == row.SourceInstanceId && parcel.SourceRunId == row.SourceRunId && parcel.SourceParcelId == row.SourceParcelId) value = parcel.BarCodes;
        value = value?.Trim();
        return string.IsNullOrEmpty(value) || value.Equals("NoRead", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    /// <summary>重量、物理体积与扫码耗时独立统计，部分量测不被其他指标的缺失值替代。</summary>
    private static DwsConsistencyGroup Summarize(DwsMeasurementSample[] samples, ParcelDwsConsistencyRequest request) {
        if (samples.Length == 1) {
            var sample = samples[0];
            var weightMetric = ParcelDwsConsistencyMath.SingleMetric(sample.WeightGrams, request.ReferenceWeightGrams);
            var volumeMetric = ParcelDwsConsistencyMath.SingleMetric(sample.VolumeCm3, request.ReferenceVolumeCm3);
            return new() { Barcode = sample.Barcode, MeasurementCount = 1, ParcelCount = sample.ParcelId is null ? 0 : 1, SourceCount = 1,
                FirstMeasuredAt = sample.MeasuredAt, LastMeasuredAt = sample.MeasuredAt,
                Weight = weightMetric, Volume = volumeMetric,
                ScanDuration = ParcelDwsConsistencyMath.SingleMetric(sample.ScanDurationMilliseconds, null),
                ReferenceDeviates = ParcelDwsConsistencyMath.Exceeds(weightMetric.MaximumReferenceDeviation, weightMetric.MaximumReferenceDeviationPercent,
                        request.WeightToleranceGrams, request.WeightTolerancePercent)
                    || ParcelDwsConsistencyMath.Exceeds(volumeMetric.MaximumReferenceDeviation, volumeMetric.MaximumReferenceDeviationPercent,
                        request.VolumeToleranceCm3, request.VolumeTolerancePercent) };
        }
        var weight = ParcelDwsConsistencyMath.Metric(samples.Select(sample => sample.WeightGrams), request.ReferenceWeightGrams);
        var volume = ParcelDwsConsistencyMath.Metric(samples.Select(sample => sample.VolumeCm3), request.ReferenceVolumeCm3);
        var scan = ParcelDwsConsistencyMath.Metric(samples.Select(sample => sample.ScanDurationMilliseconds), null);
        var times = samples.Where(sample => sample.MeasuredAt.HasValue).Select(sample => sample.MeasuredAt!.Value).Order().ToArray();
        return new() { Barcode = samples[0].Barcode, MeasurementCount = samples.Length, ParcelCount = samples.Where(sample => sample.ParcelId is not null).Select(sample => sample.ParcelId).Distinct().Count(),
            SourceCount = samples.Select(sample => sample.SourceInstanceId).Distinct(StringComparer.Ordinal).Count(), FirstMeasuredAt = times.Length > 0 ? times[0] : null,
            LastMeasuredAt = times.Length > 0 ? times[^1] : null, Weight = weight, Volume = volume, ScanDuration = scan,
            IsComparable = weight.Count >= 2 || volume.Count >= 2 || scan.Count >= 2,
            WeightDeviates = ParcelDwsConsistencyMath.Exceeds(weight.Spread, weight.SpreadPercent, request.WeightToleranceGrams, request.WeightTolerancePercent),
            VolumeDeviates = ParcelDwsConsistencyMath.Exceeds(volume.Spread, volume.SpreadPercent, request.VolumeToleranceCm3, request.VolumeTolerancePercent),
            ScanDurationDeviates = ParcelDwsConsistencyMath.Exceeds(scan.Spread, scan.SpreadPercent, request.ScanDurationToleranceMilliseconds, request.ScanDurationTolerancePercent),
            ReferenceDeviates = ParcelDwsConsistencyMath.Exceeds(weight.MaximumReferenceDeviation, weight.MaximumReferenceDeviationPercent, request.WeightToleranceGrams, request.WeightTolerancePercent)
                || ParcelDwsConsistencyMath.Exceeds(volume.MaximumReferenceDeviation, volume.MaximumReferenceDeviationPercent, request.VolumeToleranceCm3, request.VolumeTolerancePercent) };
    }

    /// <summary>按条码先比较来源中位数，再汇总偏差，不用不同大小包裹的总平均混淆设备差异。</summary>
    private static DwsConsistencySource[] Sources(DwsMeasurementSample[] samples, ParcelDwsConsistencyRequest request,
        IReadOnlyDictionary<string, DwsConsistencyGroup> overall) {
        return samples.GroupBy(sample => sample.SourceInstanceId, StringComparer.Ordinal).Select(source => {
            // 单一来源的条码复用已计算的总体，跨来源条码才另外计算来源中位数。
            var groups = source.GroupBy(sample => sample.Barcode, StringComparer.Ordinal).Where(group => overall.ContainsKey(group.Key))
                .Select(group => overall[group.Key].SourceCount == 1 ? overall[group.Key] : Summarize(group.ToArray(), request)).ToArray();
            return new DwsConsistencySource { SourceInstanceId = source.Key, WorkstationName = source.Select(sample => sample.WorkstationName).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                MeasurementCount = source.Count(), RepeatedBarcodeCount = groups.Count(group => group.MeasurementCount >= 2),
                DeviationBarcodeCount = groups.Count(group => group.WeightDeviates || group.VolumeDeviates || group.ScanDurationDeviates),
                MedianWeightDeviationPercent = ParcelDwsConsistencyMath.Median(groups.Where(group => overall[group.Barcode].SourceCount > 1)
                    .Select(group => ParcelDwsConsistencyMath.Percent(AbsDifference(group.Weight.Median, overall[group.Barcode].Weight.Median), overall[group.Barcode].Weight.Median)).Where(value => value.HasValue).Select(value => value!.Value)),
                MedianVolumeDeviationPercent = ParcelDwsConsistencyMath.Median(groups.Where(group => overall[group.Barcode].SourceCount > 1)
                    .Select(group => ParcelDwsConsistencyMath.Percent(AbsDifference(group.Volume.Median, overall[group.Barcode].Volume.Median), overall[group.Barcode].Volume.Median)).Where(value => value.HasValue).Select(value => value!.Value)),
                MedianScanDurationDeviationPercent = ParcelDwsConsistencyMath.Median(groups.Where(group => overall[group.Barcode].SourceCount > 1)
                    .Select(group => ParcelDwsConsistencyMath.Percent(AbsDifference(group.ScanDuration.Median, overall[group.Barcode].ScanDuration.Median), overall[group.Barcode].ScanDuration.Median)).Where(value => value.HasValue).Select(value => value!.Value)) };
        }).OrderByDescending(source => source.DeviationBarcodeCount).ThenByDescending(source => source.MeasurementCount).ThenBy(source => source.SourceInstanceId, StringComparer.Ordinal).ToArray();
    }

    /// <summary>未知值不补造中位数偏差。</summary>
    private static decimal? AbsDifference(decimal? left, decimal? right) => left.HasValue && right.HasValue ? Math.Abs(left.Value - right.Value) : null;

    /// <summary>拒绝过宽区间、非法阈值和无明确条码的标准参考值。</summary>
    private static ParcelDwsConsistencyRequest Validate(ParcelDwsConsistencyRequest request) {
        if (request.FromDate == default || request.ToDate == default || request.FromDate.TimeOfDay != TimeSpan.Zero || request.ToDate.TimeOfDay != TimeSpan.Zero
            || request.FromDate.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified) || request.ToDate.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified) || request.ToDate < request.FromDate
            || request.ToDate >= DateTime.MaxValue.Date || request.ToDate - request.FromDate > TimeSpan.FromDays(30)) throw new ArgumentException("请选择有效的本地日期范围，最多31天。");
        if (request.PageNumber is < 1 or > 100000 || request.MeasurementPageNumber is < 1 or > 100000 || request.SortBy is not ("weight" or "volume" or "scan-duration" or "scan-duration-p95" or "count")) throw new ArgumentException("分页或排序参数无效。");
        var tolerances = new[] { request.WeightToleranceGrams, request.WeightTolerancePercent, request.VolumeToleranceCm3, request.VolumeTolerancePercent,
            request.ScanDurationToleranceMilliseconds, request.ScanDurationTolerancePercent };
        if (tolerances.Any(value => value is < 0 or > 1000000000m) || request.ReferenceWeightGrams is <= 0 or > 1000000000m || request.ReferenceVolumeCm3 is <= 0 or > 1000000000m)
            throw new ArgumentException("阈值必须为非负有限数值，标准参考值必须大于0。");
        request = request with { Barcode = Clean(request.Barcode, 1024), DetailBarcode = Clean(request.DetailBarcode, 1024),
            SourceInstanceId = Clean(request.SourceInstanceId, 96), WorkstationName = Clean(request.WorkstationName, 128) };
        if ((request.ReferenceWeightGrams.HasValue || request.ReferenceVolumeCm3.HasValue) && request.Barcode is null) throw new ArgumentException("输入标准参考值前，请指定精确条码。");
        return request;
    }
    /// <summary>保持条码大小写，拒绝超长和不可见字符。</summary>
    private static string? Clean(string? value, int length) {
        value = value?.Trim(); if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > length || value.Any(char.IsControl)) throw new ArgumentException("条码或来源筛选内容无效。");
        return value;
    }
}
