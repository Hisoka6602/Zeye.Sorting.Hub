namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表预建计划配置。
/// </summary>
public sealed class ShardingPrebuildOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Sharding:Prebuild";

    /// <summary>
    /// 预建窗口最小值（小时）。
    /// </summary>
    public const int MinPrebuildAheadHours = 1;

    /// <summary>
    /// 预建窗口最大值（小时）。
    /// </summary>
    public const int MaxPrebuildAheadHours = 8760;

    /// <summary>
    /// 是否启用分表预建计划。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否仅输出 dry-run 计划。
    /// 可填写范围：true / false。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 向前规划预建窗口（小时）。
    /// 可填写范围：1~8760。
    /// </summary>
    public int PrebuildAheadHours { get; set; } = 72;
}
