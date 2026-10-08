using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>复用物理分表和报表预算，在数据库聚合完整总体，只返回分页标量摘要。</summary>
public sealed class ParcelAnalysisReadService(IDbContextFactory<SortingHubDbContext> factory,
    ReportingQueryBudgetPlanner budgetPlanner, ParcelPartitionStore partitions, ParcelDurationAnalysisCache? durationCache = null) : IParcelAnalysisReadService {
    /// <summary>排除未完成、缺少时间、负耗时以及来源完成时间倒序的样本。</summary>
    private static readonly Expression<Func<ParcelAnalysisSnapshot, bool>> ValidDuration = parcel =>
        parcel.Status == ParcelStatus.Completed && parcel.DetectedTime != null && parcel.CompletedTime != null
        && parcel.CompletedTime >= parcel.DetectedTime && parcel.LifecycleMilliseconds != null && parcel.LifecycleMilliseconds >= 0;

    /// <summary>包裹指标始终按首次入库日和当前快照统计，下钻条件不会改变总体分母。</summary>
    public async Task<ParcelAnalysisResponse> ReadAsync(ParcelAnalysisRequest request, CancellationToken cancellationToken) {
        request = Validate(request);
        ReportingQueryBudget budget;
        try { budget = budgetPlanner.BuildBudget(request.FromDate, request.ToDate.AddDays(1), null, false); }
        catch (InvalidOperationException exception) { throw new ArgumentException(exception.Message, nameof(request), exception); }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var catalog = await partitions.GetReadCatalogAsync(cancellationToken);
        var suffixes = catalog.Periods.Where(period => period.Start < budget.RangeEndLocal && period.End > budget.RangeStartLocal)
            .Select(period => period.Suffix).Append(string.Empty).ToArray();
        await using var read = ParcelPartitionReadContext<ParcelAnalysisSnapshot>.Create<Parcel>(db, catalog.Suffixes);
        var cohort = read.Query(suffixes, nameof(Parcel.CreatedTime), budget.RangeStartLocal, budget.RangeEndLocal, false)
            .Where(parcel => parcel.SourceParcelId != null && parcel.DetectedTime != null);
        if (request.WorkstationName is not null) cohort = cohort.Where(parcel => parcel.WorkstationName == request.WorkstationName);
        if (request.SourceInstanceId is not null) cohort = cohort.Where(parcel => parcel.SourceInstanceId == request.SourceInstanceId);
        var summary = await cohort.GroupBy(_ => 1).Select(group => new {
            Count = group.LongCount(),
            Completed = group.LongCount(parcel => parcel.Status == ParcelStatus.Completed),
            Exceptions = group.LongCount(parcel => parcel.Status == ParcelStatus.SortingException),
            NoRead = group.LongCount(parcel => parcel.NoReadType != NoReadType.None || parcel.BarCodes.ToLower() == "noread"),
            Blocked = group.LongCount(parcel => parcel.IsRoutingBlocked == true),
            Comparable = group.LongCount(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != ""
                && parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != ""),
            Mismatch = group.LongCount(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != ""
                && parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != ""
                && parcel.TargetChuteCode.Trim().ToLower() != parcel.ActualChuteCode.Trim().ToLower()),
            Fallback = group.LongCount(parcel => parcel.IsFallbackChuteAssigned == true),
            ActualChuteSamples = group.LongCount(parcel => parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != ""),
            TargetChuteSamples = group.LongCount(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != "")
        }).SingleOrDefaultAsync(cancellationToken);
        var result = new ParcelAnalysisResponse {
            View = request.View, FromDate = request.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = request.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ParcelCount = summary?.Count ?? 0, CompletedCount = summary?.Completed ?? 0,
            ExceptionCount = summary?.Exceptions ?? 0, NoReadCount = summary?.NoRead ?? 0,
            RoutingBlockedCount = summary?.Blocked ?? 0, ComparableChuteCount = summary?.Comparable ?? 0,
            ChuteMismatchCount = summary?.Mismatch ?? 0, FallbackCount = summary?.Fallback ?? 0, PageNumber = request.PageNumber
        };
        var details = cohort;
        if (request.View == "duration" && request.DurationType != "completion") {
            var duration = await ParcelDurationAnalysisReader.ReadAsync(request, cohort, db, factory, partitions, catalog, suffixes, budget, durationCache, cancellationToken);
            return result with { DurationType = request.DurationType, DurationAnalysis = duration, FilteredCount = duration.FilteredCount };
        }
        if (request.View == "exceptions") {
            var types = await cohort.Where(parcel => parcel.Status == ParcelStatus.SortingException)
                .GroupBy(parcel => parcel.ExceptionType).Select(group => new { Type = group.Key, Count = group.LongCount() })
                .OrderByDescending(row => row.Count).ThenBy(row => row.Type).ToListAsync(cancellationToken);
            result = result with { ExceptionTypes = types.Select(row => new ParcelAnalysisGroupResponse {
                Code = (int?)row.Type, Name = ExceptionName(row.Type), Count = row.Count
            }).ToArray() };
            details = request.Issue switch {
                "noread" => details.Where(parcel => parcel.NoReadType != NoReadType.None || parcel.BarCodes.ToLower() == "noread"),
                "blocked" => details.Where(parcel => parcel.IsRoutingBlocked == true),
                _ => details.Where(parcel => parcel.Status == ParcelStatus.SortingException)
            };
            if (request.Issue == "exception" && request.ExceptionType.HasValue)
                details = details.Where(parcel => parcel.ExceptionType == (ParcelExceptionType)request.ExceptionType.Value);
        } else if (request.View == "duration") {
            var valid = cohort.Where(ValidDuration);
            var durations = await valid.GroupBy(_ => 1).Select(group => new {
                Count = group.LongCount(), Sum = group.Sum(parcel => parcel.LifecycleMilliseconds!.Value),
                Minimum = group.Min(parcel => parcel.LifecycleMilliseconds!.Value), Maximum = group.Max(parcel => parcel.LifecycleMilliseconds!.Value)
            }).SingleOrDefaultAsync(cancellationToken);
            decimal? median = null; long? p95 = null;
            if (durations is { Count: > 0 }) {
                if (durations.Count > int.MaxValue) throw new ArgumentException("耗时样本过多，请缩小日期或来源范围。");
                var ordered = valid.OrderBy(parcel => parcel.LifecycleMilliseconds).ThenBy(parcel => parcel.Id)
                    .Select(parcel => parcel.LifecycleMilliseconds!.Value);
                var central = await ordered.Skip((int)((durations.Count - 1) / 2)).Take(durations.Count % 2 == 0 ? 2 : 1).ToArrayAsync(cancellationToken);
                median = central.Sum(value => (decimal)value) / central.Length;
                var rank = (int)decimal.Ceiling(durations.Count * 0.95m) - 1;
                p95 = await ordered.Skip(rank).FirstAsync(cancellationToken);
            }
            var buckets = await valid.GroupBy(parcel => parcel.LifecycleMilliseconds < 500 ? 0
                    : parcel.LifecycleMilliseconds < 1000 ? 1 : parcel.LifecycleMilliseconds < 2000 ? 2
                    : parcel.LifecycleMilliseconds < 5000 ? 3 : 4)
                .Select(group => new { Index = group.Key, Count = group.LongCount() }).ToDictionaryAsync(row => row.Index, row => row.Count, cancellationToken);
            long[] boundaries = [0, 500, 1000, 2000, 5000];
            result = result with {
                LifecycleSampleCount = durations?.Count ?? 0,
                AverageMilliseconds = durations is { Count: > 0 } ? (decimal)durations.Sum / durations.Count : null,
                MinimumMilliseconds = durations?.Minimum, MaximumMilliseconds = durations?.Maximum,
                MedianMilliseconds = median, P95Milliseconds = p95,
                DurationBuckets = boundaries.Select((minimum, index) => new ParcelDurationBucketResponse {
                    MinimumMilliseconds = minimum, MaximumMilliseconds = index < boundaries.Length - 1 ? boundaries[index + 1] : null,
                    Count = buckets.GetValueOrDefault(index)
                }).ToArray()
            };
            details = valid;
            if (request.MinimumMilliseconds.HasValue) details = details.Where(parcel => parcel.LifecycleMilliseconds >= request.MinimumMilliseconds);
            if (request.MaximumMilliseconds.HasValue) details = details.Where(parcel => parcel.LifecycleMilliseconds < request.MaximumMilliseconds);
        } else {
            var rowLimit = Math.Min(1000, budget.RowLimit);
            var routes = await cohort.GroupBy(parcel => new { parcel.SourceInstanceId, parcel.WorkstationName, parcel.TargetChuteCode, parcel.ActualChuteCode })
                .Select(group => new ParcelChuteRouteResponse {
                    SourceInstanceId = group.Key.SourceInstanceId, WorkstationName = group.Key.WorkstationName,
                    TargetChuteCode = group.Key.TargetChuteCode, ActualChuteCode = group.Key.ActualChuteCode,
                    Count = group.LongCount(), FallbackCount = group.LongCount(parcel => parcel.IsFallbackChuteAssigned == true)
                }).OrderByDescending(route => route.Count).ThenBy(route => route.SourceInstanceId).ThenBy(route => route.WorkstationName)
                .ThenBy(route => route.TargetChuteCode).ThenBy(route => route.ActualChuteCode).Take(rowLimit + 1).ToArrayAsync(cancellationToken);
            result = result with { ChuteRoutes = routes.Take(rowLimit).ToArray(), ChuteRoutesTruncated = routes.Length > rowLimit };
            result = result with {
                ActualChuteHeatmap = await ParcelChuteHeatmapReader.ReadAsync(cohort, true, summary?.ActualChuteSamples ?? 0,
                    result.ParcelCount, rowLimit, cancellationToken),
                TargetChuteHeatmap = await ParcelChuteHeatmapReader.ReadAsync(cohort, false, summary?.TargetChuteSamples ?? 0,
                    result.ParcelCount, rowLimit, cancellationToken)
            };
            if (request.MismatchOnly) details = details.Where(parcel => parcel.TargetChuteCode != null && parcel.TargetChuteCode.Trim() != ""
                && parcel.ActualChuteCode != null && parcel.ActualChuteCode.Trim() != ""
                && parcel.TargetChuteCode.Trim().ToLower() != parcel.ActualChuteCode.Trim().ToLower());
            if (request.FallbackOnly) details = details.Where(parcel => parcel.IsFallbackChuteAssigned == true);
            if (request.TargetChuteCode is not null) details = details.Where(parcel => parcel.TargetChuteCode == request.TargetChuteCode);
            if (request.ActualChuteCode is not null) details = details.Where(parcel => parcel.ActualChuteCode == request.ActualChuteCode);
        }
        var filteredCount = await details.LongCountAsync(cancellationToken);
        var orderedDetails = request.View == "duration"
            ? details.OrderByDescending(parcel => parcel.LifecycleMilliseconds).ThenByDescending(parcel => parcel.Id)
            : details.OrderByDescending(parcel => parcel.CreatedTime).ThenByDescending(parcel => parcel.Id);
        var items = await orderedDetails.Skip((request.PageNumber - 1) * 20).Take(20).ToArrayAsync(cancellationToken);
        return result with { FilteredCount = filteredCount, Items = items.Select(parcel => new ParcelAnalysisParcelResponse {
            Id = parcel.Id.ToString(CultureInfo.InvariantCulture), BarCodes = parcel.BarCodes, CreatedTime = parcel.CreatedTime,
            ScannedTime = parcel.ScannedTime, SourceInstanceId = parcel.SourceInstanceId, WorkstationName = parcel.WorkstationName,
            Status = (int)parcel.Status, ExceptionName = parcel.Status == ParcelStatus.SortingException ? ExceptionName(parcel.ExceptionType) : null,
            LifecycleMilliseconds = parcel.Status == ParcelStatus.Completed && parcel.CompletedTime >= parcel.DetectedTime
                && parcel.LifecycleMilliseconds >= 0 ? parcel.LifecycleMilliseconds : null,
            TargetChuteCode = parcel.TargetChuteCode, ActualChuteCode = parcel.ActualChuteCode,
            IsFallbackChuteAssigned = parcel.IsFallbackChuteAssigned, IsRoutingBlocked = parcel.IsRoutingBlocked
        }).ToArray() };
    }

    /// <summary>在创建任何数据库查询前限制日期、分页和精确筛选字段。</summary>
    private static ParcelAnalysisRequest Validate(ParcelAnalysisRequest request) {
        if (request.View is not ("exceptions" or "duration" or "chutes")) throw new ArgumentException("不支持的包裹分析类型。");
        if (!ParcelDurationAnalysisReader.Types.ContainsKey(request.DurationType)) throw new ArgumentException("不支持的耗时类型。");
        if (request.FromDate.TimeOfDay != TimeSpan.Zero || request.ToDate.TimeOfDay != TimeSpan.Zero
            || request.ToDate < request.FromDate || request.ToDate >= DateTime.MaxValue.Date)
            throw new ArgumentException("请选择有效的本地日期范围。");
        if (request.PageNumber is < 1 or > 100000) throw new ArgumentException("页码必须在1至100000之间。");
        if (request.Issue is not ("exception" or "noread" or "blocked")) throw new ArgumentException("不支持的异常下钻范围。");
        if (request.ExceptionType.HasValue && !Enum.IsDefined(typeof(ParcelExceptionType), request.ExceptionType.Value))
            throw new ArgumentException("异常类型无效。");
        if (request.MinimumMilliseconds is < 0 || request.MaximumMilliseconds is <= 0
            || request.MinimumMilliseconds.HasValue && request.MaximumMilliseconds <= request.MinimumMilliseconds)
            throw new ArgumentException("耗时区间必须为非负下限和更大的上限，单位毫秒。");
        return request with {
            WorkstationName = Normalize(request.WorkstationName, 128), SourceInstanceId = Normalize(request.SourceInstanceId, 96),
            TargetChuteCode = Normalize(request.TargetChuteCode, 128, false), ActualChuteCode = Normalize(request.ActualChuteCode, 128, false)
        };
    }

    /// <summary>精确查询参数不接受超长值，空白值表示不筛选；格口下钻保留原始编码。</summary>
    private static string? Normalize(string? value, int maximum, bool trim = true) {
        if (value?.Length > maximum) throw new ArgumentException("筛选字段超过允许长度。");
        return string.IsNullOrWhiteSpace(value) ? null : trim ? value.Trim() : value;
    }

    /// <summary>异常中文名称来自领域枚举，避免前端枚举过期。</summary>
    private static string ExceptionName(ParcelExceptionType? type) => type.HasValue
        ? typeof(ParcelExceptionType).GetField(type.Value.ToString())?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>().FirstOrDefault()?.Description ?? type.Value.ToString()
        : "未提供异常类型";
}
