using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>按明确来源和真实处理事实读取DWS、阶段及接口耗时，不改写写入热路径。</summary>
internal static class ParcelDurationAnalysisReader {
    /// <summary>小总体只传入一次有界编号集合；大总体按覆盖索引读取，避免重复扫描日期范围。</summary>
    internal const int FactBatchSize = 512;
    /// <summary>只读取协议的分类和尝试标识，不把请求正文当作元数据。</summary>
    private static readonly HashSet<string> MetadataNames = new(["kind", "name", "category", "provider", "operation", "operationId", "attemptId", "attemptNumber", "outcome", "status", "outcomeLevel", "businessAccepted", "detail"], StringComparer.OrdinalIgnoreCase);
    /// <summary>固定类型、真实端点和计数单位的口径目录。</summary>
    internal static readonly IReadOnlyDictionary<string, (string Name, string Description, bool Calls)> Types =
        new Dictionary<string, (string, string, bool)>(StringComparer.Ordinal) {
            ["completion"] = ("完成耗时", "首次检测至实际完成，仅统计时间有效的已完成包裹。", false),
            ["dws"] = ("DWS 获取耗时", "来源创建包裹（首次检测）至首次成功关联 DWS 信息；不使用设备测量时间或 Hub 入库时间替代获取时间。", false),
            ["routing"] = ("格口决策耗时", "首次成功关联 DWS 信息至首次成功分配目标格口。", false),
            ["sorting"] = ("分拣执行耗时", "首次成功下发分拣指令至实际落格。", false),
            ["scan-upload"] = ("扫描上传接口", "按独立扫描上传调用统计，失败和重试均保留；调用诊断不重复计算。", true),
            ["chute-request"] = ("请求格口接口", "按独立请求格口调用统计，明确区别于扫描上传与本地格口决策。", true),
            ["landing-report"] = ("落格回传接口", "按独立落格回传调用统计，与设备实际落格耗时分开。", true),
            ["image-upload"] = ("图片上传接口", "按独立图片上传调用统计，重复尝试单独计数。", true),
            ["other-api"] = ("其他接口", "锁格、解锁、集包等其他接口，以及尚未明确业务分类的独立调用。", true)
        };

    /// <summary>完整总体统计与分页下钻分开，区间切换复用相同样本。</summary>
    internal static async Task<ParcelDurationAnalysisResponse> ReadAsync(ParcelAnalysisRequest request, IQueryable<ParcelAnalysisSnapshot> cohort,
        SortingHubDbContext db, IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions,
        ParcelPartitionCatalogSnapshot catalog, string[] suffixes, ReportingQueryBudget budget,
        ParcelDurationAnalysisCache? cache, CancellationToken cancellationToken) {
        var key = JsonSerializer.Serialize(new { request.DurationType, request.FromDate, request.ToDate, request.WorkstationName, request.SourceInstanceId,
            Catalog = string.Join('|', suffixes) });
        Task<ParcelDurationDataset> Load() => LoadAsync(request, cohort, db, factory, partitions, catalog, suffixes, budget, cancellationToken);
        var data = cache is null ? await Load() : await cache.GetAsync(key, request.RefreshDurationSnapshot, Load, cancellationToken);
        if (data.ByType is not null) data = data.ByType[request.DurationType];
        var type = Types[request.DurationType];
        var values = data.Samples.Select(sample => sample.Milliseconds).Order().ToArray();
        var filtered = data.Samples.Where(sample => (!request.MinimumMilliseconds.HasValue || sample.Milliseconds >= request.MinimumMilliseconds)
            && (!request.MaximumMilliseconds.HasValue || sample.Milliseconds < request.MaximumMilliseconds)).ToArray();
        var interfaces = type.Calls ? data.Samples.GroupBy(sample => (sample.Provider, sample.RequestUrl))
            .Select(group => new ParcelDurationInterfaceResponse {
                Provider = group.Key.Provider, RequestUrl = group.Key.RequestUrl, Count = group.LongCount(),
                FailedCount = group.LongCount(sample => sample.IsSuccess == false),
                AverageMilliseconds = group.Average(sample => sample.Milliseconds),
                P95Milliseconds = Quantile95(group.Select(sample => sample.Milliseconds).Order().ToArray())!.Value
            }).OrderByDescending(group => group.Count).ThenBy(group => group.Provider).ThenBy(group => group.RequestUrl).ToArray() : [];
        long[] edges = [0, 100, 500, 1000, 2000, 5000];
        var bucketCounts = new long[edges.Length];
        foreach (var value in values) { var index = edges.Length - 1; while (index > 0 && value < edges[index]) index--; bucketCounts[index]++; }
        return new() {
            Type = request.DurationType, Name = type.Name, Description = type.Description, Unit = type.Calls ? "次" : "票",
            GeneratedAt = data.GeneratedAt, ObservedCount = data.ObservedCount, UnavailableCount = data.UnavailableCount,
            SampleCount = values.LongLength, ParcelCount = data.Samples.Select(sample => sample.ParcelId).Distinct().LongCount(),
            FailedCount = type.Calls ? data.Samples.LongCount(sample => sample.IsSuccess == false) : 0,
            UnknownResultCount = type.Calls ? data.Samples.LongCount(sample => sample.IsSuccess is null) : 0,
            AverageMilliseconds = values.Length > 0 ? values.Average() : null,
            MedianMilliseconds = values.Length > 0 ? (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2m : null,
            P95Milliseconds = Quantile95(values), MinimumMilliseconds = values.FirstOrDefaultNullable(), MaximumMilliseconds = values.LastOrDefaultNullable(),
            Buckets = edges.Select((edge, index) => new ParcelDurationBucketResponse {
                MinimumMilliseconds = edge, MaximumMilliseconds = index + 1 < edges.Length ? edges[index + 1] : null, Count = bucketCounts[index]
            }).ToArray(), Interfaces = interfaces.Take(100).ToArray(), InterfacesTruncated = interfaces.Length > 100,
            FilteredCount = filtered.LongLength,
            Items = filtered.OrderByDescending(sample => sample.Milliseconds).ThenBy(sample => sample.Key, StringComparer.Ordinal)
                .Skip((request.PageNumber - 1) * 20).Take(20).ToArray()
        };
    }

    /// <summary>空总体最小耗时保持未知。</summary>
    private static decimal? FirstOrDefaultNullable(this decimal[] values) => values.Length == 0 ? null : values[0];
    /// <summary>空总体最大耗时保持未知。</summary>
    private static decimal? LastOrDefaultNullable(this decimal[] values) => values.Length == 0 ? null : values[^1];
    /// <summary>P95使用最近秩，不用当前分页代替总体。</summary>
    private static decimal? Quantile95(decimal[] values) => values.Length == 0 ? null : values[(int)decimal.Ceiling(values.Length * .95m) - 1];

    /// <summary>EF Core读取所选日期物理分表，仅关联身份一致的事实。</summary>
    private static async Task<ParcelDurationDataset> LoadAsync(ParcelAnalysisRequest request, IQueryable<ParcelAnalysisSnapshot> cohort,
        SortingHubDbContext db, IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions,
        ParcelPartitionCatalogSnapshot catalog, string[] suffixes, ReportingQueryBudget budget, CancellationToken cancellationToken) {
        // 只读标量身份与时间，完整报文和Owned集合不进入分析总体。
        var parcels = await cohort.Select(parcel => new ParcelAnalysisSnapshot {
            Id = parcel.Id, CreatedTime = parcel.CreatedTime, DetectedTime = parcel.DetectedTime, BarCodes = parcel.BarCodes,
            SourceInstanceId = parcel.SourceInstanceId, SourceRunId = parcel.SourceRunId, SourceParcelId = parcel.SourceParcelId,
            WorkstationName = parcel.WorkstationName
        }).ToDictionaryAsync(parcel => parcel.Id, cancellationToken);
        if (parcels.Count == 0) return new(DateTime.Now, 0, 0, []);
        var parcelIds = parcels.Count <= FactBatchSize ? parcels.Keys.ToArray() : null;
        var calls = Types[request.DurationType].Calls;
        ParcelProcessingStage[] stages = calls ? [ParcelProcessingStage.ScanUploaded, ParcelProcessingStage.LandingReported, ParcelProcessingStage.ImageUploaded]
            : request.DurationType == "dws" ? [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound]
            : request.DurationType == "routing" ? [ParcelProcessingStage.DwsBound, ParcelProcessingStage.ChuteAssigned]
            : [ParcelProcessingStage.SorterDispatched, ParcelProcessingStage.SortingCompleted];
        await using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, catalog.Suffixes, streaming: true);
        await using var narrow = calls ? ParcelPartitionReadContext<ParcelDurationFact>.Create<ParcelDurationFact>(db, catalog.Suffixes, streaming: true) : null;
        await using var canonical = calls ? ParcelPartitionReadContext<ParcelDurationCall>.Create<ParcelDurationCall>(db, catalog.Suffixes, streaming: true) : null;
        var indexed = calls ? (await db.Set<ParcelDurationBackfillState>().AsNoTracking().Where(row => row.Completed && suffixes.Contains(row.Suffix))
            .Select(row => row.Suffix).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal) : [];
        var scoped = new List<ParcelDurationFactSnapshot>();
        var callEvents = new List<ParcelDurationCallEvent>();
        var observations = new List<ParcelDurationCall>();
        // 分表内先筛选并投影，再流式读取；不能让UNION派生表先物化完整LONGTEXT报文。
        foreach (var suffix in suffixes) {
            if (calls && indexed.Contains(suffix)) {
                var indexedFacts = narrow!.Query([suffix], nameof(ParcelDurationFact.PartitionTime), budget.RangeStartLocal, budget.RangeEndLocal, false)
                    .Where(row => row.ParcelId != null);
                if (parcelIds is not null) indexedFacts = indexedFacts.Where(row => parcelIds.Contains(row.ParcelId!.Value));
                if (request.SourceInstanceId is not null) indexedFacts = indexedFacts.Where(row => row.SourceInstanceId == request.SourceInstanceId);
                var pending = await indexedFacts.Where(row => !row.Projected).Select(row => row.ParcelId!.Value).Distinct()
                    .Take(FactBatchSize + 1).ToArrayAsync(cancellationToken);
                if (pending.Length <= FactBatchSize) {
                    // 类型在物理表内裁剪，少量图片或其他调用不再搬运全部扫描、路由和回传样本。
                    var ready = canonical!.Query([suffix], nameof(ParcelDurationCall.PartitionTime), budget.RangeStartLocal, budget.RangeEndLocal, false)
                        .Where(row => row.Type == request.DurationType);
                    if (parcelIds is not null) ready = ready.Where(row => parcelIds.Contains(row.ParcelId));
                    if (request.SourceInstanceId is not null) ready = ready.Where(row => row.SourceInstanceId == request.SourceInstanceId);
                    if (pending.Length > 0) ready = ready.Where(row => !pending.Contains(row.ParcelId));
                    await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(ready, budget.RangeStartLocal, budget.RangeEndLocal, cancellationToken))
                    await foreach (var row in window.AsAsyncEnumerable().WithCancellation(cancellationToken)) {
                        if (parcels.TryGetValue(row.ParcelId, out var parcel) && parcel.SourceInstanceId == row.SourceInstanceId
                            && parcel.SourceRunId == row.SourceRunId && parcel.SourceParcelId == row.SourceParcelId) observations.Add(row);
                    }
                    if (pending.Length == 0) continue;
                    // 少量新增或迟到诊断即时归并，不能为等待后台而给出过时或不完整的强制刷新结果。
                    indexedFacts = indexedFacts.Where(row => pending.Contains(row.ParcelId!.Value));
                }
                await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(indexedFacts, budget.RangeStartLocal, budget.RangeEndLocal, cancellationToken))
                await foreach (var row in window.AsAsyncEnumerable().WithCancellation(cancellationToken)) {
                    if (parcels.TryGetValue(row.ParcelId!.Value, out var parcel) && parcel.SourceInstanceId == row.SourceInstanceId
                        && parcel.SourceRunId == row.SourceRunId && parcel.SourceParcelId == row.SourceParcelId) callEvents.Add(new(row));
                }
                continue;
            }
            // 只在一次性历史补齐尚未完成时兼容原事实；禁止把不完整投影误当成完整统计。
            var facts = read.Query([suffix], nameof(ParcelProcessingRecord.PartitionTime), budget.RangeStartLocal, budget.RangeEndLocal, false);
            // 阶段等值在索引上分别定位窗口；多阶段 IN 或 UNION 后排序会反复扫描剩余大范围。
            foreach (var stage in stages) {
                IQueryable<ParcelDurationFactSnapshot> QueryWindow(DateTime from, DateTime to) =>
                    BuildFactProjection(ParcelAnalysisWindowReader.Window(facts, from, to), [stage], calls, request.SourceInstanceId, parcelIds);
                await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(QueryWindow, budget.RangeStartLocal, budget.RangeEndLocal, cancellationToken))
                await foreach (var record in window.AsAsyncEnumerable().WithCancellation(cancellationToken)) {
                    if (!parcels.TryGetValue(record.ParcelId!.Value, out var parcel)
                        || parcel.SourceInstanceId != record.SourceInstanceId || parcel.SourceRunId != record.SourceRunId
                        || parcel.SourceParcelId != record.SourceParcelId) continue;
                    if (calls) callEvents.Add(new(record, Metadata(record))); else scoped.Add(record);
                }
            }
        }
        var samples = new List<ParcelDurationSampleResponse>();
        long observed = 0, unavailable = 0;
        if (!calls) {
            var byParcel = scoped.ToLookup(record => record.ParcelId!.Value);
            foreach (var parcel in parcels.Values) {
                observed++;
                var history = byParcel[parcel.Id].OrderBy(record => record.OccurredAt).ThenBy(record => record.RecordId).ToArray();
                var firstDws = history.FirstOrDefault(record => record.Stage == ParcelProcessingStage.DwsBound && record.IsSuccess == true);
                var startRecord = request.DurationType == "dws" ? history.FirstOrDefault(record => record.Stage == ParcelProcessingStage.Detected && record.OccurredAt == parcel.DetectedTime)
                    : request.DurationType == "routing" ? firstDws
                    : history.FirstOrDefault(record => record.Stage == ParcelProcessingStage.SorterDispatched && record.IsSuccess == true);
                var start = request.DurationType == "dws" ? parcel.DetectedTime : startRecord?.OccurredAt;
                var endRecord = request.DurationType == "dws" ? firstDws : request.DurationType == "routing"
                    ? history.FirstOrDefault(record => record.Stage == ParcelProcessingStage.ChuteAssigned && record.IsSuccess == true)
                    : history.FirstOrDefault(record => record.Stage == ParcelProcessingStage.SortingCompleted && record.IsSuccess != false);
                var end = endRecord?.OccurredAt;
                if (start is null || end is null || start == default(DateTime) || end < start
                    || endRecord?.HasReliableTimestamp == false || startRecord?.HasReliableTimestamp == false) { unavailable++; continue; }
                samples.Add(Sample(parcel, request.DurationType + ":" + parcel.Id, start, end, (end.Value - start.Value).Ticks / (decimal)TimeSpan.TicksPerMillisecond, "时间端点", null, null, null, true));
            }
        } else {
            var apiSamples = Types.Where(type => type.Value.Calls).ToDictionary(type => type.Key, _ => new List<ParcelDurationSampleResponse>(), StringComparer.Ordinal);
            var apiObserved = apiSamples.Keys.ToDictionary(type => type, _ => 0L, StringComparer.Ordinal);
            var apiUnavailable = apiSamples.Keys.ToDictionary(type => type, _ => 0L, StringComparer.Ordinal);
            void Observe(string type) { observed++; apiObserved[type]++; }
            void Missing(string type) { unavailable++; apiUnavailable[type]++; }
            void Keep(string type, ParcelDurationSampleResponse sample) { samples.Add(sample); apiSamples[type].Add(sample); }
            observations.AddRange(ParcelDurationObservationBuilder.Build(callEvents));
            foreach (var row in observations.Where(row => row.Type == request.DurationType)) {
                Observe(row.Type);
                if (!row.Milliseconds.HasValue) { Missing(row.Type); continue; }
                Keep(row.Type, Sample(parcels[row.ParcelId], row.SampleKey, row.StartedAt, row.EndedAt, row.Milliseconds.Value,
                    row.TimingSource, row.Provider, row.RequestUrl, row.AttemptNumber, row.IsSuccess));
            }
            // 兼容既有Owned接口记录，按物理分表读取，不按请求日期截断晚到的调用。
            var seenRequests = apiSamples.SelectMany(group => group.Value.Select(sample => (group.Key, Sample: sample)))
                .GroupBy(item => (Type: item.Key, item.Sample.ParcelId, item.Sample.StartedAt, item.Sample.EndedAt, item.Sample.RequestUrl))
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (var suffix in suffixes) {
                await using var context = await partitions.CreateContextAsync(suffix, cancellationToken);
                var owned = context.Set<Parcel>().AsNoTracking().Where(parcel => parcel.CreatedTime >= budget.RangeStartLocal && parcel.CreatedTime < budget.RangeEndLocal
                    && parcel.SourceParcelId != null && parcel.DetectedTime != null);
                if (request.SourceInstanceId is not null) owned = owned.Where(parcel => parcel.SourceInstanceId == request.SourceInstanceId);
                if (request.WorkstationName is not null) owned = owned.Where(parcel => parcel.WorkstationName == request.WorkstationName);
                var requests = owned.SelectMany(parcel => parcel.ApiRequests, (parcel, api) => new {
                    ParcelId = parcel.Id, Id = EF.Property<long>(api, "Id"), api.ApiType, api.RequestStatus, api.RequestTime, api.ResponseTime, api.ElapsedMilliseconds, api.RequestUrl
                });
                var known = new[] { ApiRequestType.ScanResult, ApiRequestType.RequestChute, ApiRequestType.DischargeReport, ApiRequestType.UploadImage };
                var selected = request.DurationType switch {
                    "scan-upload" => ApiRequestType.ScanResult, "chute-request" => ApiRequestType.RequestChute,
                    "landing-report" => ApiRequestType.DischargeReport, "image-upload" => ApiRequestType.UploadImage, _ => (ApiRequestType?)null
                };
                requests = selected.HasValue ? requests.Where(api => api.ApiType == selected.Value) : requests.Where(api => !known.Contains(api.ApiType));
                await foreach (var api in requests.AsAsyncEnumerable().WithCancellation(cancellationToken)) {
                    if (!parcels.ContainsKey(api.ParcelId)) continue;
                    var type = api.ApiType switch {
                        ApiRequestType.ScanResult => "scan-upload", ApiRequestType.RequestChute => "chute-request",
                        ApiRequestType.DischargeReport => "landing-report", ApiRequestType.UploadImage => "image-upload", _ => "other-api"
                    };
                    var url = SafeUrl(api.RequestUrl);
                    var identity = (type, api.ParcelId.ToString(CultureInfo.InvariantCulture), (DateTime?)api.RequestTime, api.ResponseTime, url);
                    // 只与事实记录一对一去重；同时间的两个独立Owned调用仍分别保留。
                    if (seenRequests.TryGetValue(identity, out var duplicates) && duplicates > 0) { seenRequests[identity] = duplicates - 1; continue; }
                    Observe(type);
                    var reported = api.ResponseTime.HasValue || api.ElapsedMilliseconds > 0 ? api.ElapsedMilliseconds : (int?)null;
                    if (!TryDuration(api.RequestTime, api.ResponseTime, reported, null, out var elapsed, out var source)) { Missing(type); continue; }
                    Keep(type, Sample(parcels[api.ParcelId], "legacy:" + suffix + ":" + api.Id, api.RequestTime, api.ResponseTime, elapsed, source, null, url, null,
                        api.RequestStatus == ApiRequestStatus.Success ? true : api.RequestStatus == ApiRequestStatus.Failed ? false : null));
                }
            }
            var generatedAt = DateTime.Now;
            return new(generatedAt, observed, unavailable, samples) {
                ByType = apiSamples.ToDictionary(group => group.Key,
                    group => new ParcelDurationDataset(generatedAt, apiObserved[group.Key], apiUnavailable[group.Key], group.Value), StringComparer.Ordinal)
            };
        }
        return new(DateTime.Now, observed, unavailable, samples);
    }

    /// <summary>在单个物理表内裁剪LOB字段：阶段不读取报文，接口只读取最多2KiB分类元数据。</summary>
    internal static IQueryable<ParcelDurationFactSnapshot> BuildFactProjection(IQueryable<ParcelDurationFactSnapshot> facts,
        ParcelProcessingStage[] stages, bool calls, string? sourceInstanceId, long[]? parcelIds = null) {
        if (parcelIds is { Length: > FactBatchSize }) throw new ArgumentException("耗时事实的包裹编号超过单批上限。", nameof(parcelIds));
        // 指定来源时用各阶段的等值覆盖索引分支合并；避免提供器将多阶段IN改走来源索引并回表读取宽事实。
        if (!calls && sourceInstanceId is not null && stages.Length > 1)
            return stages.Distinct().Select(stage => BuildFactProjection(facts, [stage], false, sourceInstanceId, parcelIds))
                .Aggregate((left, right) => left.Concat(right));
        facts = facts.Where(record => record.ParcelId != null);
        if (stages.Length == 1) { var stage = stages[0]; facts = facts.Where(record => record.Stage == stage); }
        else facts = facts.Where(record => stages.Contains(record.Stage));
        if (parcelIds is not null) facts = facts.Where(record => parcelIds.Contains(record.ParcelId!.Value));
        if (sourceInstanceId is not null) facts = facts.Where(record => record.SourceInstanceId == sourceInstanceId);
        // 阶段查询仅投影真实使用的十列；不给 UNION 追加接口 NULL 列，避免放大驱动行元数据及取数缓冲。
        if (!calls) return facts.Select(record => new ParcelDurationFactSnapshot {
            RecordId = record.RecordId, ParcelId = record.ParcelId, SourceInstanceId = record.SourceInstanceId,
            SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId, PartitionTime = record.PartitionTime,
            OccurredAt = record.OccurredAt, Stage = record.Stage, IsSuccess = record.IsSuccess, HasReliableTimestamp = record.HasReliableTimestamp
        });
        return facts.Select(record => new ParcelDurationFactSnapshot {
            RecordId = record.RecordId, ParcelId = record.ParcelId, SourceInstanceId = record.SourceInstanceId,
            SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId, PartitionTime = record.PartitionTime,
            OccurredAt = record.OccurredAt, Stage = record.Stage, IsSuccess = record.IsSuccess, HasReliableTimestamp = record.HasReliableTimestamp,
            AttemptNumber = record.AttemptNumber, Provider = record.Provider, RequestUrl = record.RequestUrl,
            RequestAt = record.RequestAt, ResponseAt = record.ResponseAt,
            ElapsedMilliseconds = record.ElapsedMilliseconds, ErrorMessage = record.ErrorMessage,
            // Fusion把操作身份完整保存在detail/ErrorMessage；原文前部只需kind、Provider和category，避免重复搬运转义后的detail。
            RawPayload = record.RawPayload != null ? record.RawPayload.Substring(0,
                record.ErrorMessage != null && record.ErrorMessage.StartsWith("{\"operationId\"") ? 512 : 2048) : null
        });
    }

    /// <summary>真实端点优先，来源上报只补充耗时，不倒推端点；倒序和不可靠时间拒绝。</summary>
    internal static bool TryDuration(DateTime? start, DateTime? end, int? reported, bool? reliable, out decimal elapsed, out string source) {
        elapsed = 0; source = "时间端点";
        if (reliable == false || start == default(DateTime) || end == default(DateTime) || reported < 0 || end < start) return false;
        if (start.HasValue && end.HasValue) { elapsed = (end.Value - start.Value).Ticks / (decimal)TimeSpan.TicksPerMillisecond; return true; }
        if (reported.HasValue) { elapsed = reported.Value; source = "来源上报"; return true; }
        return false;
    }

    /// <summary>样本保留包裹和尝试身份，允许追溯原始详情。</summary>
    private static ParcelDurationSampleResponse Sample(ParcelAnalysisSnapshot parcel, string key, DateTime? start, DateTime? end, decimal elapsed,
        string source, string? provider, string? url, int? attempt, bool? success) => new() {
        Key = key + ":" + parcel.Id.ToString(CultureInfo.InvariantCulture), ParcelId = parcel.Id.ToString(CultureInfo.InvariantCulture), BarCodes = parcel.BarCodes, WorkstationName = parcel.WorkstationName,
        SourceInstanceId = parcel.SourceInstanceId, StartedAt = start, EndedAt = end, Milliseconds = elapsed, TimingSource = source,
        Provider = provider, RequestUrl = url, AttemptNumber = attempt, IsSuccess = success
    };

    /// <summary>接口统计不返回地址中的凭据、查询参数或片段。</summary>
    internal static string? SafeUrl(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)) { var safe = new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }; return safe.Uri.GetLeftPart(UriPartial.Path); }
        return value.Split('?', '#')[0];
    }

    /// <summary>读取有界JSON前部的顶层字段，截断正文不妨碍之前的元数据；不扫描正文猜业务。</summary>
    internal static Dictionary<string, string> Metadata(ParcelDurationFactSnapshot record) {
        var data = ReadMetadata(record.RawPayload);
        if (data.TryGetValue("detail", out var detail)) foreach (var pair in ReadMetadata(detail)) data[pair.Key] = pair.Value;
        foreach (var pair in ReadMetadata(record.ErrorMessage)) data[pair.Key] = pair.Value;
        return data;
    }

    /// <summary>读取完整JSON或截断JSON前部已完成的顶层字段，忽略正文内同名字段。</summary>
    private static Dictionary<string, string> ReadMetadata(string? value) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return result;
        try {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(value), false, default);
            while (reader.Read()) {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;
                var name = reader.GetString()!;
                if (!reader.Read()) break;
                if (MetadataNames.Contains(name) && reader.TokenType is JsonTokenType.String or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Number)
                    result[name] = reader.TokenType == JsonTokenType.String ? reader.GetString()! : reader.TokenType == JsonTokenType.Number
                        ? reader.GetDecimal().ToString(CultureInfo.InvariantCulture) : reader.TokenType == JsonTokenType.True ? "true" : "false";
            }
        } catch (JsonException) { }
        return result;
    }

}
