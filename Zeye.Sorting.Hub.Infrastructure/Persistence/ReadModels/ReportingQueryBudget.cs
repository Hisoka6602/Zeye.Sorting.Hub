namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>
/// 报表查询预算快照。
/// </summary>
public readonly record struct ReportingQueryBudget {
    /// <summary>
    /// 初始化报表查询预算快照。
    /// </summary>
    /// <param name="rangeStartLocal">查询开始时间。</param>
    /// <param name="rangeEndLocal">查询结束时间。</param>
    /// <param name="rowLimit">返回行数上限。</param>
    /// <param name="includeTotalCount">是否返回总数。</param>
    public ReportingQueryBudget(DateTime rangeStartLocal, DateTime rangeEndLocal, int rowLimit, bool includeTotalCount) {
        RangeStartLocal = rangeStartLocal;
        RangeEndLocal = rangeEndLocal;
        RowLimit = rowLimit;
        IncludeTotalCount = includeTotalCount;
    }

    /// <summary>
    /// 查询开始时间（本地时间语义）。
    /// </summary>
    public DateTime RangeStartLocal { get; init; }

    /// <summary>
    /// 查询结束时间（本地时间语义）。
    /// </summary>
    public DateTime RangeEndLocal { get; init; }

    /// <summary>
    /// 返回行数上限。
    /// </summary>
    public int RowLimit { get; init; }

    /// <summary>
    /// 是否返回总数。
    /// </summary>
    public bool IncludeTotalCount { get; init; }
}
