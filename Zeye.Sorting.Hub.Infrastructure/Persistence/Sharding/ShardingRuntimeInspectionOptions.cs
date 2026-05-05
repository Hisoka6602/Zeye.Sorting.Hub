namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表运行期巡检配置。
/// </summary>
public sealed class ShardingRuntimeInspectionOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Sharding:RuntimeInspection";

    /// <summary>
    /// 巡检间隔最小值（分钟）。
    /// </summary>
    public const int MinInspectionIntervalMinutes = 1;

    /// <summary>
    /// 巡检间隔最大值（分钟）。
    /// </summary>
    public const int MaxInspectionIntervalMinutes = 1440;

    /// <summary>
    /// 是否启用运行期分表巡检。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 巡检间隔（分钟）。
    /// 可填写范围：1~1440。
    /// </summary>
    public int InspectionIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// 是否检查关键索引。
    /// 可填写范围：true / false。
    /// </summary>
    public bool ShouldCheckIndexes { get; set; } = true;

    /// <summary>
    /// 是否检查下一周期物理分表。
    /// 可填写范围：true / false。
    /// </summary>
    public bool ShouldCheckNextPeriodTables { get; set; } = true;

    /// <summary>
    /// 是否检查容量与热点风险。
    /// 可填写范围：true / false。
    /// </summary>
    public bool ShouldCheckCapacity { get; set; } = true;
}
