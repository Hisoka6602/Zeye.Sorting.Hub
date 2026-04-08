using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 闭环阶段跟踪器（显式阶段流转，最多保留最近 <see cref="MaxStageHistory"/> 条记录）。
/// <para>
/// 线程安全契约：此类<b>非</b>线程安全，<see cref="MoveTo"/> 与 <see cref="Stages"/>
/// 必须由同一线程顺序调用，不得并发访问。
/// </para>
/// </summary>
public sealed class AutoTuningClosedLoopTracker {
    private const int MaxStageHistory = 1000;

    /// <summary>
    /// 闭环阶段迁移历史记录列表，仅供单一后台线程读写。
    /// </summary>
    private readonly List<AutoTuningClosedLoopStage> _stages = new();

    /// <summary>
    /// 初始化闭环阶段跟踪器。
    /// </summary>
    public AutoTuningClosedLoopTracker() {
        _stages.Add(AutoTuningClosedLoopStage.Monitor);
    }

    /// <summary>
    /// 阶段历史（只读视图，调用方不得在迭代时并发修改）。
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
