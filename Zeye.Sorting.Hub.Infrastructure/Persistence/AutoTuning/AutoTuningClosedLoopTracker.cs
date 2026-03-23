using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 闭环阶段跟踪器（显式阶段流转，最多保留最近 <see cref="MaxStageHistory"/> 条记录）。
/// </summary>
public sealed class AutoTuningClosedLoopTracker {
    private const int MaxStageHistory = 1000;

    /// <summary>
    /// 字段：_stages。
    /// </summary>
    private readonly List<AutoTuningClosedLoopStage> _stages = new();

    /// <summary>
    /// 初始化闭环阶段跟踪器。
    /// </summary>
    public AutoTuningClosedLoopTracker() {
        _stages.Add(AutoTuningClosedLoopStage.Monitor);
    }

    /// <summary>
    /// 阶段历史。
    /// </summary>
    public IReadOnlyList<AutoTuningClosedLoopStage> Stages => _stages;

    /// <summary>
    /// 迁移到目标阶段。
    /// </summary>
    public void MoveTo(AutoTuningClosedLoopStage stage) {
        if (_stages[^1] == stage) {
            return;
        }

        if (_stages.Count >= MaxStageHistory) {
            _stages.RemoveAt(0);
        }

        _stages.Add(stage);
    }
}
