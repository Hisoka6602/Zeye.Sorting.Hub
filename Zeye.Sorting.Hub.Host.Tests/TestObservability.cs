using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 自动调优观测接口测试桩，用于收集指标与事件输出。
/// </summary>
internal sealed class TestObservability : IAutoTuningObservability {
    /// <summary>
    /// 收集 EmitMetric 的指标名称序列，用于断言关键指标是否被发出。
    /// </summary>
    public readonly List<string> Metrics = [];

    /// <summary>
    /// 收集 EmitEvent 的“事件名:消息”序列，用于断言关键治理事件是否被发出。
    /// </summary>
    public readonly List<string> Events = [];

    /// <summary>
    /// 收集 EmitMetric 的完整入参快照（名称/值/标签），用于精确断言指标内容。
    /// </summary>
    public readonly List<ObservabilityEntry> MetricEntries = [];

    /// <summary>
    /// 收集 EmitEvent 的名称与标签快照（不含 level/message），用于断言事件标签与事件名映射。
    /// 事件消息文本由 <see cref="Events"/> 集合单独收集并用于消息断言。
    /// </summary>
    public readonly List<ObservabilityEntry> EventEntries = [];

    /// <summary>
    /// 验证场景：EmitMetric。
    /// </summary>
    public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
        Metrics.Add(name);
        MetricEntries.Add(new ObservabilityEntry(name, value, CloneTags(tags)));
    }

    /// <summary>
    /// 验证场景：EmitEvent。
    /// </summary>
    public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
        Events.Add($"{name}:{message}");
        EventEntries.Add(new ObservabilityEntry(name, 0d, CloneTags(tags)));
    }

    /// <summary>
    /// 验证场景：CloneTags。
    /// </summary>
    private static IReadOnlyDictionary<string, string> CloneTags(IReadOnlyDictionary<string, string>? tags) {
        return tags is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase);
    }
}
