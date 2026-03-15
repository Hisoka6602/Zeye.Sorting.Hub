using Microsoft.Extensions.Logging;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>自动调优观测输出抽象（日志/指标统一入口）。</summary>
    public interface IAutoTuningObservability {
        void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
        void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null);
    }

    /// <summary>默认空实现（允许在未注册观测实现时保持兼容）。</summary>
    public sealed class NullAutoTuningObservability : IAutoTuningObservability {
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
        }

        public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
        }
    }

    /// <summary>闭环自治阶段模型。</summary>
    public enum AutoTuningClosedLoopStage {
        Monitor,
        Diagnose,
        Execute,
        Verify,
        Rollback
    }

    /// <summary>回归检测结果（支持 unavailable 标记）。</summary>
    public sealed record RegressionEvaluationResult(
        bool IsRegressed,
        bool IsSevereRegression,
        string Reason,
        string LockWaitStatus);

    /// <summary>危险动作隔离决策。</summary>
    public enum ActionIsolationDecision {
        Execute,
        BlockedByGuard,
        DryRunOnly
    }

    /// <summary>危险动作隔离策略引擎。</summary>
    public static class ActionIsolationPolicy {
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

    /// <summary>自动回滚决策。</summary>
    public static class AutoRollbackDecisionEngine {
        public static RegressionEvaluationResult Evaluate(
            decimal p99IncreasePercent,
            decimal timeoutRateIncreasePercent,
            string lockWaitStatus,
            decimal severeRollbackP99IncreasePercent,
            decimal severeRollbackTimeoutIncreasePercent,
            bool regressed,
            string reason) {
            var severeRegression = (severeRollbackP99IncreasePercent > 0m && p99IncreasePercent >= severeRollbackP99IncreasePercent)
                || (severeRollbackTimeoutIncreasePercent > 0m && timeoutRateIncreasePercent >= severeRollbackTimeoutIncreasePercent);
            return new RegressionEvaluationResult(regressed, severeRegression, reason, lockWaitStatus);
        }
    }

    /// <summary>闭环阶段跟踪器（显式阶段流转）。</summary>
    public sealed class AutoTuningClosedLoopTracker {
        private readonly List<AutoTuningClosedLoopStage> _stages = new();

        public AutoTuningClosedLoopTracker() {
            _stages.Add(AutoTuningClosedLoopStage.Monitor);
        }

        public IReadOnlyList<AutoTuningClosedLoopStage> Stages => _stages;

        public void MoveTo(AutoTuningClosedLoopStage stage) {
            if (_stages[^1] == stage) {
                return;
            }

            _stages.Add(stage);
        }
    }

    /// <summary>执行计划回退检测抽象（后续可接数据库计划视图）。</summary>
    public interface IExecutionPlanRegressionProbe {
        PlanRegressionSnapshot Evaluate(string sqlFingerprint);
    }

    /// <summary>执行计划回退快照（默认 unavailable）。</summary>
    public sealed record PlanRegressionSnapshot(
        bool IsAvailable,
        bool IsRegressed,
        string Summary);

    /// <summary>默认执行计划探针：仅输出 unavailable 占位。</summary>
    public sealed class LoggingOnlyExecutionPlanRegressionProbe : IExecutionPlanRegressionProbe {
        public PlanRegressionSnapshot Evaluate(string sqlFingerprint) {
            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={sqlFingerprint}, plan regression unavailable");
        }
    }
}
