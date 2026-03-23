namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 日分表预建日期解析结果。
/// </summary>
/// <param name="PrebuiltDates">已预建日期集合（本地日期）。</param>
/// <param name="ValidationErrors">解析错误集合。</param>
internal readonly record struct PrebuiltPerDayShardDatesResolution(
    IReadOnlySet<DateTime> PrebuiltDates,
    IReadOnlyList<string> ValidationErrors);
