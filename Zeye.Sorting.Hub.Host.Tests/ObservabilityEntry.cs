namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 观测项测试记录：统一承载指标/事件名、数值与标签快照。
/// </summary>
internal sealed record ObservabilityEntry(string Name, double Value, IReadOnlyDictionary<string, string> Tags);
