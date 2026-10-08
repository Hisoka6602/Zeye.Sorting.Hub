using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>时序查询覆盖已登记分表和历史基础表，仅读取11票的时序字段。</summary>
public sealed class ParcelTimingReadService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore? partitions = null) : IParcelTimingReadService {
    /// <summary>固定单侧邻近票数。</summary>
    private const int NeighbourCount = 5;
    /// <summary>重复条码候选页大小。</summary>
    private const int CandidatePageSize = 20;
    /// <summary>数据库内只投影身份、状态与真实摘要时间。</summary>
    private static readonly Expression<Func<ParcelTimingSnapshot, ParcelTimingCandidateResponse>> Candidate = parcel => new() {
        Id = parcel.Id.ToString(), BarCodes = parcel.BarCodes, WorkstationName = parcel.WorkstationName,
        SourceInstanceId = parcel.SourceInstanceId, SourceRunId = parcel.SourceRunId,
        SourceParcelId = parcel.SourceParcelId.HasValue ? parcel.SourceParcelId.Value.ToString() : null,
        Status = (int)parcel.Status, ScannedTime = parcel.ScannedTime, DetectedTime = parcel.DetectedTime, CreatedTime = parcel.CreatedTime,
        DischargeTime = parcel.DischargeTime, CompletedTime = parcel.CompletedTime
    };

    /// <summary>数字条码与包裹Id同时命中时也保留所有候选；历史记录不受默认24小时窗口限制。</summary>
    public async Task<ParcelTimingCandidatesResponse> SearchAsync(string query, string searchBy, int pageNumber, CancellationToken cancellationToken) {
        query = query?.Trim() ?? string.Empty;
        if (query.Length is 0 or > 1024) throw new ArgumentException("请输入不超过1024字符的包裹 ID 或完整条码。");
        if (searchBy is not ("auto" or "id" or "barcode")) throw new ArgumentException("查询方式必须为 auto、id 或 barcode。");
        if (pageNumber is < 1 or > 1000) throw new ArgumentException("候选页码必须介于1和1000之间。");
        var validId = long.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0;
        if (searchBy == "id" && !validId) throw new ArgumentException("包裹 ID 必须为有效的正64位整数。");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var suffixes = await SuffixesAsync(cancellationToken);
        await using var read = ParcelPartitionReadContext<ParcelTimingSnapshot>.Create<Parcel>(db, suffixes);
        var matchId = searchBy != "barcode" && validId;
        var matchBarcode = searchBy != "id";
        var matches = read.QueryAll(suffixes, branch => branch.Where(parcel => matchId && parcel.Id == id || matchBarcode && parcel.BarCodes == query));
        var count = await matches.LongCountAsync(cancellationToken);
        var items = await matches.OrderByDescending(parcel => parcel.ScannedTime).ThenByDescending(parcel => parcel.Id)
            .Skip((pageNumber - 1) * CandidatePageSize).Take(CandidatePageSize).Select(Candidate).ToArrayAsync(cancellationToken);
        return new() { Items = items, TotalCount = count, PageNumber = pageNumber, PageSize = CandidatePageSize };
    }

    /// <summary>数据库内稳定选取两侧各5票，锚点只加入一次，缺少邻近数据时不补造记录。</summary>
    public async Task<ParcelTimingResponse?> ReadAsync(long id, CancellationToken cancellationToken) {
        if (id <= 0) throw new ArgumentException("包裹 ID 必须大于0。");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var suffixes = await SuffixesAsync(cancellationToken);
        await using var read = ParcelPartitionReadContext<ParcelTimingSnapshot>.Create<Parcel>(db, suffixes);
        var anchor = await read.QueryAll(suffixes, branch => branch.Where(parcel => parcel.Id == id)).Select(Candidate).SingleOrDefaultAsync(cancellationToken);
        if (anchor is null) return null;
        var before = await read.QueryAll(suffixes, branch => branch.Where(parcel => parcel.ScannedTime < anchor.ScannedTime || parcel.ScannedTime == anchor.ScannedTime && parcel.Id < id)
            .OrderByDescending(parcel => parcel.ScannedTime).ThenByDescending(parcel => parcel.Id).Take(NeighbourCount))
            .OrderByDescending(parcel => parcel.ScannedTime).ThenByDescending(parcel => parcel.Id).Take(NeighbourCount).Select(Candidate).ToListAsync(cancellationToken);
        before.Reverse();
        var after = await read.QueryAll(suffixes, branch => branch.Where(parcel => parcel.ScannedTime > anchor.ScannedTime || parcel.ScannedTime == anchor.ScannedTime && parcel.Id > id)
            .OrderBy(parcel => parcel.ScannedTime).ThenBy(parcel => parcel.Id).Take(NeighbourCount))
            .OrderBy(parcel => parcel.ScannedTime).ThenBy(parcel => parcel.Id).Take(NeighbourCount).Select(Candidate).ToArrayAsync(cancellationToken);
        var summaries = before.Append(anchor).Concat(after).ToArray();
        return new() {
            AnchorId = anchor.Id, BeforeCount = before.Count, AfterCount = after.Length,
            Items = await ReadTimingsAsync(db, summaries, cancellationToken)
        };
    }

    /// <summary>有界对比保持输入顺序，使用分表联合投影与批量事实查询，不装载图片及量测明细集合。</summary>
    public async Task<ParcelComparisonResponse> CompareAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken) {
        if (ids.Count is < 1 or > 8 || ids.Any(id => id <= 0)) throw new ArgumentException("请选择1至8票有效的包裹 ID。");
        var selected = ids.Distinct().ToArray();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var suffixes = await SuffixesAsync(cancellationToken);
        await using var read = ParcelPartitionReadContext<ParcelTimingSnapshot>.Create<Parcel>(db, suffixes);
        var parcels = read.QueryAll(suffixes, branch => branch.Where(parcel => selected.Contains(parcel.Id)));
        var summaries = await parcels.Where(parcel => selected.Contains(parcel.Id)).Select(Candidate).ToArrayAsync(cancellationToken);
        var byId = summaries.ToDictionary(parcel => parcel.Id, StringComparer.Ordinal);
        var requested = selected.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray();
        var ordered = requested.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
        var measurements = await parcels.Where(parcel => selected.Contains(parcel.Id)).Select(parcel => new {
            Id = parcel.Id.ToString(), parcel.Weight, parcel.Length, parcel.Width, parcel.Height, parcel.Volume,
            parcel.TargetChuteCode, parcel.ActualChuteCode
        }).ToDictionaryAsync(parcel => parcel.Id, cancellationToken);
        // 查询期间发生清理时，按缺失包裹返回，不能对已删除快照索引抛出异常。
        ordered = ordered.Where(parcel => measurements.ContainsKey(parcel.Id)).ToArray();
        var found = ordered.Select(parcel => parcel.Id).ToHashSet(StringComparer.Ordinal);
        ParcelTimingParcelResponse[] timings = ordered.Length == 0 ? [] : await ReadTimingsAsync(db, ordered, cancellationToken);
        return new() {
            RequestedIds = requested, MissingIds = requested.Where(id => !found.Contains(id)).ToArray(),
            Items = timings.Select(timing => {
                var measurement = measurements[timing.Id];
                return new ParcelComparisonItemResponse {
                    Timing = timing, Weight = measurement.Weight, Length = measurement.Length,
                    Width = measurement.Width, Height = measurement.Height, Volume = measurement.Volume,
                    TargetChuteCode = measurement.TargetChuteCode, ActualChuteCode = measurement.ActualChuteCode
                };
            }).ToArray()
        };
    }

    /// <summary>共用已选包裹的事实读取，邻票时序和多包裹对比使用相同的归组与报文精简规则。</summary>
    private async Task<ParcelTimingParcelResponse[]> ReadTimingsAsync(SortingHubDbContext db, ParcelTimingCandidateResponse[] summaries, CancellationToken cancellationToken) {
        var ids = summaries.Select(parcel => long.Parse(parcel.Id, CultureInfo.InvariantCulture)).ToArray();
        var suffixes = await SuffixesAsync(cancellationToken);
        await using var read = ParcelPartitionReadContext<ParcelTimingFactSnapshot>.Create<ParcelProcessingRecord>(db, suffixes, streaming: true);
        var recordQuery = read.QueryAll(suffixes, branch => branch.Where(record => record.ParcelId.HasValue && ids.Contains(record.ParcelId.Value)));
        // 报文正文、图片和量测集合不进入甘特查询，避免11次完整聚合读取。
        var records = await recordQuery.Where(record => record.ParcelId.HasValue && ids.Contains(record.ParcelId.Value))
            .OrderBy(record => record.OccurredAt).ThenBy(record => record.RecordId).Select(record => new ParcelProcessingRecordResponse {
                RecordId = record.RecordId, ParcelId = record.ParcelId!.Value.ToString(),
                SourceInstanceId = record.SourceInstanceId, SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId,
                Stage = (int)record.Stage, OccurredAt = record.OccurredAt, RecordedAt = record.RecordedAt,
                PartitionTime = record.PartitionTime, IsSuccess = record.IsSuccess, AttemptNumber = record.AttemptNumber,
                Provider = record.Provider, RawPayload = record.RawPayload, ErrorMessage = record.ErrorMessage,
                DecisionReason = record.DecisionReason, RequestAt = record.RequestAt, ResponseAt = record.ResponseAt,
                ElapsedMilliseconds = record.ElapsedMilliseconds, TargetChuteCode = record.TargetChuteCode,
                ActualChuteCode = record.ActualChuteCode, HasReliableTimestamp = record.HasReliableTimestamp
            }).ToArrayAsync(cancellationToken);
        var byId = summaries.ToDictionary(parcel => parcel.Id, StringComparer.Ordinal);
        var scopedRecords = records.Where(record => record.ParcelId is not null && byId.TryGetValue(record.ParcelId, out var parcel)
            && record.SourceInstanceId == parcel.SourceInstanceId && record.SourceRunId == parcel.SourceRunId
            && record.SourceParcelId?.ToString(CultureInfo.InvariantCulture) == parcel.SourceParcelId)
            .Select(record => record with { RawPayload = CompactDiagnostic(record.RawPayload), ErrorMessage = CompactError(record.ErrorMessage) })
            .ToLookup(record => record.ParcelId!, StringComparer.Ordinal);
        var legacyRequests = await ReadLegacyRequestsAsync(db, ids, cancellationToken);
        return summaries.Select(parcel => new ParcelTimingParcelResponse {
                Id = parcel.Id, BarCodes = parcel.BarCodes, WorkstationName = parcel.WorkstationName,
                SourceInstanceId = parcel.SourceInstanceId, SourceRunId = parcel.SourceRunId, SourceParcelId = parcel.SourceParcelId,
                Status = parcel.Status, ScannedTime = parcel.ScannedTime, DetectedTime = parcel.DetectedTime, CreatedTime = parcel.CreatedTime,
                DischargeTime = parcel.DischargeTime, CompletedTime = parcel.CompletedTime,
                ProcessingRecords = scopedRecords[parcel.Id].ToArray(),
                ApiRequests = legacyRequests.GetValueOrDefault(parcel.Id) ?? []
            }).ToArray();
    }

    /// <summary>合并已登记的物理表与历史基础表。</summary>
    private async Task<IReadOnlyList<string>> SuffixesAsync(CancellationToken cancellationToken) =>
        partitions is null ? [string.Empty] : (await partitions.GetReadCatalogAsync(cancellationToken)).Suffixes;

    /// <summary>Owned接口记录必须使用所属物理表的模型读取；只投影时间，不自动加载其他Owned集合。</summary>
    private async Task<Dictionary<string, ParcelTimingApiRequestResponse[]>> ReadLegacyRequestsAsync(SortingHubDbContext db, long[] ids, CancellationToken cancellationToken) {
        var locations = partitions is null ? new Dictionary<long, string>()
            : await db.Set<ParcelLocation>().Where(location => ids.Contains(location.Id)).ToDictionaryAsync(location => location.Id, location => location.Suffix, cancellationToken);
        var result = new Dictionary<string, ParcelTimingApiRequestResponse[]>(StringComparer.Ordinal);
        foreach (var group in ids.GroupBy(id => locations.GetValueOrDefault(id) ?? string.Empty)) {
            await using var context = partitions is null ? await factory.CreateDbContextAsync(cancellationToken) : await partitions.CreateContextAsync(group.Key, cancellationToken);
            var groupIds = group.ToArray();
            var requests = await context.Set<Parcel>().AsNoTracking().Where(parcel => groupIds.Contains(parcel.Id))
                .SelectMany(parcel => parcel.ApiRequests, (parcel, request) => new {
                    parcel.Id, Timing = new ParcelTimingApiRequestResponse {
                        ApiType = (int)request.ApiType, RequestStatus = (int)request.RequestStatus,
                        RequestTime = request.RequestTime, ResponseTime = request.ResponseTime, ElapsedMilliseconds = request.ElapsedMilliseconds
                    }
                }).ToArrayAsync(cancellationToken);
            foreach (var parcel in requests.GroupBy(request => request.Id))
                result[parcel.Key.ToString(CultureInfo.InvariantCulture)] = parcel.Select(request => request.Timing).ToArray();
        }
        return result;
    }

    /// <summary>诊断元数据精简为归组字段，普通错误文字保持原义。</summary>
    private static string? CompactError(string? value) {
        if (value?.TrimStart().StartsWith('{') != true) return value;
        try {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.EnumerateObject()
                .Any(property => property.Name.Equals("operationId", StringComparison.OrdinalIgnoreCase)))
                return CompactDiagnostic(value);
        }
        catch (JsonException) { }
        return value;
    }

    /// <summary>仅保留轨迹归组需要的标识、业务名称和结果，报文原文仍在原始事实与详情接口。</summary>
    private static string? CompactDiagnostic(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var names = new HashSet<string>(["kind", "outcomeLevel", "name", "category", "provider", "operation", "operationId", "attemptId", "attemptNumber", "outcome", "status", "businessAccepted"], StringComparer.OrdinalIgnoreCase);
            var data = document.RootElement.EnumerateObject().Where(property => names.Contains(property.Name))
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
            foreach (var name in new[] { "detail", "response" }) {
                var nested = document.RootElement.EnumerateObject().FirstOrDefault(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                var content = nested.Value;
                JsonDocument? parsed = null;
                try {
                    if (content.ValueKind == JsonValueKind.String) {
                        var text = content.GetString();
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        parsed = JsonDocument.Parse(text);
                        content = parsed.RootElement;
                    }
                    if (content.ValueKind != JsonValueKind.Object) continue;
                    var fields = content.EnumerateObject().Where(property => names.Contains(property.Name)
                        || name == "response" && property.Name is "isAccepted" or "IsAccepted" or "isAssigned" or "IsAssigned"
                        || name == "response" && property.Name.Equals("rawResponse", StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "Scan upload disabled by config.")
                        .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
                    data[nested.Name] = JsonSerializer.SerializeToElement(fields);
                }
                // Fusion调用开始常带空响应；普通文本或截断正文也不能抹掉其他调用元数据。
                catch (JsonException) { }
                finally { parsed?.Dispose(); }
            }
            return JsonSerializer.Serialize(data);
        }
        catch (JsonException) { return null; }
    }
}
