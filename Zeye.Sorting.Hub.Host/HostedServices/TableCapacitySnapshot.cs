namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 表级容量快照（CapturedLocalTime 必须使用本地时间语义）。
/// </summary>
internal sealed record TableCapacitySnapshot(
    DateTime CapturedLocalTime,
    long AffectedRows,
    int CallCount);
