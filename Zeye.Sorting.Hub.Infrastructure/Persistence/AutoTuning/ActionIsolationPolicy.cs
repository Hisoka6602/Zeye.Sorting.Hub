using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 危险动作隔离策略引擎。
/// </summary>
public static class ActionIsolationPolicy {
    /// <summary>
    /// 评估危险动作执行决策。
    /// </summary>
    public static ActionIsolationDecision Evaluate(
        bool enableGuard,
        bool allowDangerousActionExecution,
        bool enableDryRun,
        bool dangerousAction,
        bool isRollback) {
        if (enableGuard && dangerousAction && !allowDangerousActionExecution && !isRollback) {
            return ActionIsolationDecision.BlockedByGuard;
        }

        if (enableDryRun && !isRollback) {
            return ActionIsolationDecision.DryRunOnly;
        }

        return ActionIsolationDecision.Execute;
    }
}
