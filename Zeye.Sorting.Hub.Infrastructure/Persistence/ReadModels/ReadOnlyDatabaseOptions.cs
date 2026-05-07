namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>
/// 报表查询只读数据库配置。
/// </summary>
public sealed class ReadOnlyDatabaseOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:ReadOnlyDatabase";

    /// <summary>
    /// 报表查询最大时间范围最小天数。
    /// </summary>
    public const int MinMaxReportTimeRangeDays = 1;

    /// <summary>
    /// 报表查询最大时间范围最大天数。
    /// </summary>
    public const int MaxMaxReportTimeRangeDays = 366;

    /// <summary>
    /// 报表查询最大返回行数最小值。
    /// </summary>
    public const int MinMaxReportRows = 1;

    /// <summary>
    /// 报表查询最大返回行数最大值。
    /// </summary>
    public const int MaxMaxReportRows = 100000;

    /// <summary>
    /// 是否启用只读数据库路由。
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 只读数据库不可用时是否回退主库。
    /// </summary>
    public bool FallbackToPrimaryWhenUnavailable { get; set; }

    /// <summary>
    /// 报表查询允许的最大时间范围（天）。
    /// </summary>
    public int MaxReportTimeRangeDays { get; set; } = 31;

    /// <summary>
    /// 报表查询允许的最大返回行数。
    /// </summary>
    public int MaxReportRows { get; set; } = 10000;
}
