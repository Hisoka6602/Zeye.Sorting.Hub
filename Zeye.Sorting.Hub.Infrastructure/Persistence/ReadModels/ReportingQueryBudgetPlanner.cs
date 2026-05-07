using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>
/// 报表查询预算规划器。
/// </summary>
public sealed class ReportingQueryBudgetPlanner {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 只读数据库配置。
    /// </summary>
    private readonly ReadOnlyDatabaseOptions _options;

    /// <summary>
    /// 初始化报表查询预算规划器。
    /// </summary>
    /// <param name="options">只读数据库配置。</param>
    public ReportingQueryBudgetPlanner(IOptions<ReadOnlyDatabaseOptions> options) {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 构建报表查询预算。
    /// </summary>
    /// <param name="rangeStartLocal">查询开始时间。</param>
    /// <param name="rangeEndLocal">查询结束时间。</param>
    /// <param name="requestedRows">请求返回行数。</param>
    /// <param name="includeTotalCount">是否请求总数。</param>
    /// <returns>预算快照。</returns>
    public ReportingQueryBudget BuildBudget(DateTime rangeStartLocal, DateTime rangeEndLocal, int? requestedRows, bool includeTotalCount) {
        var normalizedRangeStartLocal = NormalizeLocalBoundary(rangeStartLocal, nameof(rangeStartLocal));
        var normalizedRangeEndLocal = NormalizeLocalBoundary(rangeEndLocal, nameof(rangeEndLocal));
        if (normalizedRangeEndLocal < normalizedRangeStartLocal) {
            Logger.Error(
                "报表查询预算校验失败：结束时间早于开始时间，RangeStartLocal={RangeStartLocal}, RangeEndLocal={RangeEndLocal}",
                normalizedRangeStartLocal,
                normalizedRangeEndLocal);
            throw new InvalidOperationException(
                $"报表查询结束时间不能早于开始时间. RangeStartLocal={normalizedRangeStartLocal:yyyy-MM-dd HH:mm:ss}, RangeEndLocal={normalizedRangeEndLocal:yyyy-MM-dd HH:mm:ss}.");
        }

        var range = normalizedRangeEndLocal - normalizedRangeStartLocal;
        if (range.TotalDays > _options.MaxReportTimeRangeDays) {
            Logger.Error(
                "报表查询预算校验失败：时间范围超限，RangeDays={RangeDays}, MaxReportTimeRangeDays={MaxReportTimeRangeDays}",
                range.TotalDays,
                _options.MaxReportTimeRangeDays);
            throw new InvalidOperationException($"报表查询时间范围不能超过 {_options.MaxReportTimeRangeDays} 天。");
        }

        var rowLimit = requestedRows is > 0
            ? Math.Min(requestedRows.Value, _options.MaxReportRows)
            : _options.MaxReportRows;
        if (requestedRows is > 0 && requestedRows.Value > _options.MaxReportRows) {
            Logger.Warn(
                "报表查询请求行数超出预算，RequestedRows={RequestedRows}, MaxReportRows={MaxReportRows}",
                requestedRows.Value,
                _options.MaxReportRows);
        }

        if (includeTotalCount) {
            Logger.Warn("报表查询请求总数统计已被预算规划器关闭。");
        }

        return new ReportingQueryBudget(
            rangeStartLocal: normalizedRangeStartLocal,
            rangeEndLocal: normalizedRangeEndLocal,
            rowLimit: rowLimit,
            includeTotalCount: false);
    }

    /// <summary>
    /// 归一本地时间边界。
    /// </summary>
    /// <param name="value">时间值。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>归一化后的本地时间。</returns>
    private static DateTime NormalizeLocalBoundary(DateTime value, string parameterName) {
        if (value.Kind != DateTimeKind.Local && value.Kind != DateTimeKind.Unspecified) {
            Logger.Error("报表查询时间参数使用了 UTC 语义，ParameterName={ParameterName}", parameterName);
            throw new InvalidOperationException("报表查询时间参数必须使用本地时间语义。");
        }

        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Local)
            : value;
    }
}
