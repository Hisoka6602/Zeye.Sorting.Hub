using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>在数据库内按本地日期聚合真实包裹快照与独立处理事实。</summary>
public sealed class ParcelAnalyticsReadService : IParcelAnalyticsReadService {
    /// <summary>报表范围验证日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>基础模型上下文工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>跨历史粒度的物理表目录。</summary>
    private readonly ParcelPartitionStore _partitions;
    /// <summary>查询时间及返回行数预算。</summary>
    private readonly ReportingQueryBudgetPlanner _budgetPlanner;

    /// <summary>组装报表只读服务。</summary>
    public ParcelAnalyticsReadService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions, ReportingQueryBudgetPlanner budgetPlanner) {
        _factory = factory;
        _partitions = partitions;
        _budgetPlanner = budgetPlanner;
    }

    /// <summary>以首次入库日为包裹总体，以发生日为处理事实总体；两个总体不混用。</summary>
    public async Task<ParcelAnalyticsResponse> GetAsync(DateTime fromLocalDate, DateTime toLocalDate, CancellationToken cancellationToken) {
        if (fromLocalDate.TimeOfDay != TimeSpan.Zero || toLocalDate.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("报表范围必须使用本地日期，不接受时分秒。");
        if (toLocalDate < fromLocalDate || toLocalDate == DateTime.MaxValue.Date)
            throw new ArgumentException("报表日期范围无效。");
        var endExclusive = toLocalDate.AddDays(1);
        ReportingQueryBudget budget;
        try {
            budget = _budgetPlanner.BuildBudget(fromLocalDate, endExclusive, null, false);
        }
        catch (InvalidOperationException exception) {
            Logger.Warn(exception, "包裹报表查询范围超出预算，FromDate={FromDate}, ToDate={ToDate}", fromLocalDate, toLocalDate);
            throw new ArgumentException(exception.Message, nameof(toLocalDate), exception);
        }
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var parcels = (await ParcelPartitionQueryBuilder.BuildParcelsByCreatedTimeAsync(db,
            budget.RangeStartLocal, budget.RangeEndLocal, cancellationToken))
            .Where(x => x.CreatedTime >= budget.RangeStartLocal && x.CreatedTime < budget.RangeEndLocal
                && x.SourceParcelId != null && x.DetectedTime != null);

        // 步骤1：数据库执行条件计数，空时间窗口没有任何伪造的日数据或零秒平均值。
        var dayRows = await parcels.GroupBy(x => x.CreatedTime.Date).Select(group => new {
            Date = group.Key,
            DetectedCount = group.LongCount(),
            CompletedCount = group.LongCount(x => x.Status == ParcelStatus.Completed),
            ExceptionCount = group.LongCount(x => x.Status == ParcelStatus.SortingException),
            NoReadCount = group.LongCount(x => x.NoReadType != NoReadType.None || x.BarCodes.ToLower() == "noread"),
            ChuteMismatchCount = group.LongCount(x => x.TargetChuteCode != null && x.ActualChuteCode != null
                && x.TargetChuteCode.ToLower() != x.ActualChuteCode.ToLower()),
            LifecycleSampleCount = group.LongCount(x => x.Status == ParcelStatus.Completed && x.LifecycleMilliseconds != null),
            LifecycleMilliseconds = group.Sum(x => x.Status == ParcelStatus.Completed && x.LifecycleMilliseconds != null
                ? x.LifecycleMilliseconds.Value : 0L)
        }).OrderBy(x => x.Date).ToListAsync(cancellationToken);

        var daily = dayRows.Select(x => new ParcelAnalyticsDailyItem {
            Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DetectedCount = x.DetectedCount,
            CompletedCount = x.CompletedCount,
            ExceptionCount = x.ExceptionCount,
            NoReadCount = x.NoReadCount,
            ChuteMismatchCount = x.ChuteMismatchCount,
            LifecycleSampleCount = x.LifecycleSampleCount,
            AverageLifecycleSeconds = x.LifecycleSampleCount == 0 ? null : (decimal)x.LifecycleMilliseconds / x.LifecycleSampleCount / 1000m
        }).ToArray();
        var lifecycleSamples = dayRows.Sum(x => x.LifecycleSampleCount);
        var lifecycleMilliseconds = dayRows.Sum(x => x.LifecycleMilliseconds);

        // 步骤2：工作台和异常类型先在数据库分组，返回行数受既有报表预算控制。
        var exceptionRows = await parcels.Where(x => x.Status == ParcelStatus.SortingException)
            .GroupBy(x => x.ExceptionType)
            .Select(group => new { Type = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).ToListAsync(cancellationToken);
        var workstationRows = await parcels.GroupBy(x => x.WorkstationName)
            .Select(group => new { Name = group.Key, Count = group.LongCount() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).Take(budget.RowLimit + 1)
            .ToListAsync(cancellationToken);
        var workstationsTruncated = workstationRows.Count > budget.RowLimit;
        var workstations = workstationRows.Take(budget.RowLimit).Select(x => new ParcelAnalyticsDistributionItem {
            Code = x.Name,
            Name = string.IsNullOrWhiteSpace(x.Name) ? "未提供" : x.Name,
            Count = x.Count
        }).ToArray();
        var exceptionTypes = exceptionRows.Select(x => new ParcelAnalyticsDistributionItem {
            Code = x.Type?.ToString(),
            Name = x.Type.HasValue ? GetDescription(x.Type.Value) : "未提供",
            Count = x.Count
        }).ToArray();

        // 步骤3：处理事实按事件发生时间独立计数；失败尝试和未绑定消息不除以包裹件数。
        var records = (await ParcelPartitionQueryBuilder.BuildAsync<ParcelProcessingRecord>(db, _partitions, cancellationToken))
            .Where(x => x.OccurredAt >= budget.RangeStartLocal && x.OccurredAt < budget.RangeEndLocal);
        var events = await records.GroupBy(_ => 1).Select(group => new {
            Count = group.LongCount(),
            Failed = group.LongCount(x => x.IsSuccess == false),
            UnboundDws = group.LongCount(x => x.ParcelId == null
                && (x.Stage == ParcelProcessingStage.DwsReceived || x.Stage == ParcelProcessingStage.DwsBound))
        }).SingleOrDefaultAsync(cancellationToken);

        return new ParcelAnalyticsResponse {
            FromDate = fromLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = toLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DetectedCount = daily.Sum(x => x.DetectedCount),
            CompletedCount = daily.Sum(x => x.CompletedCount),
            ExceptionCount = daily.Sum(x => x.ExceptionCount),
            NoReadCount = daily.Sum(x => x.NoReadCount),
            ChuteMismatchCount = daily.Sum(x => x.ChuteMismatchCount),
            AverageLifecycleSeconds = lifecycleSamples == 0 ? null : (decimal)lifecycleMilliseconds / lifecycleSamples / 1000m,
            Daily = daily,
            ExceptionTypes = exceptionTypes,
            Workstations = workstations,
            WorkstationsTruncated = workstationsTruncated,
            ProcessingEventCount = events?.Count ?? 0,
            FailedAttemptCount = events?.Failed ?? 0,
            UnboundDwsEventCount = events?.UnboundDws ?? 0
        };
    }

    /// <summary>使用领域枚举中文说明展示异常，不用硬编码前端分类。</summary>
    private static string GetDescription(ParcelExceptionType type) {
        var field = typeof(ParcelExceptionType).GetField(type.ToString());
        return field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .OfType<DescriptionAttribute>().FirstOrDefault()?.Description ?? type.ToString();
    }
}
