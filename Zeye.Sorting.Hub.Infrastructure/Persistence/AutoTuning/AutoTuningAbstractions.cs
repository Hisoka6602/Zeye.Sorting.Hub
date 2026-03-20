using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Domain.Enums;

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

        /// <summary>
        /// 空实现：不输出任何事件观测数据，用于禁用观测链路时保持调用兼容。
        /// </summary>
        public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
        }
    }

    /// <summary>回归检测结果（支持 unavailable 标记）。</summary>
    public sealed record RegressionEvaluationResult(
        bool IsRegressed,
        bool IsSevereRegression,
        string Reason,
        string LockWaitStatus);

    /// <summary>自动验证单项指标对比快照。</summary>
    public sealed record AutoTuningVerificationMetricDiff(
        string Name,
        string Status,
        string Baseline,
        string Current,
        string Delta,
        string Reason);

    /// <summary>自动验证标准化输出结构。</summary>
    public sealed record AutoTuningVerificationResult(
        string Verdict,
        string Reason,
        IReadOnlyList<AutoTuningVerificationMetricDiff> SnapshotDiff);

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

    /// <summary>闭环阶段跟踪器（显式阶段流转，最多保留最近 <see cref="MaxStageHistory"/> 条记录）。</summary>
    public sealed class AutoTuningClosedLoopTracker {
        private const int MaxStageHistory = 1000;
        /// <summary>
        /// 字段：_stages。
        /// </summary>
        private readonly List<AutoTuningClosedLoopStage> _stages = new();

        public AutoTuningClosedLoopTracker() {
            _stages.Add(AutoTuningClosedLoopStage.Monitor);
        }

        public IReadOnlyList<AutoTuningClosedLoopStage> Stages => _stages;

        /// <summary>
        /// 执行逻辑：MoveTo。
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

    /// <summary>执行计划回退检测抽象（后续可接数据库计划视图）。</summary>
    public interface IExecutionPlanRegressionProbe {
        PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint);
    }

    /// <summary>
    /// provider-aware 执行计划回退探针扩展请求（为未来真实 EXPLAIN/SHOWPLAN 实现预留上下文）。
    /// </summary>
    /// <param name="ProviderName">数据库提供器名称。</param>
    /// <param name="SqlFingerprint">标准化 SQL 指纹。</param>
    public readonly record struct ExecutionPlanProbeRequest(
        string ProviderName,
        string SqlFingerprint);

    /// <summary>
    /// provider-aware 执行计划回退探针扩展约定。
    /// </summary>
    /// <remarks>
    /// 当前默认实现仍为 logging-only；真实数据库级计划探针可在该接口下受控接入，
    /// 不改变现有隔离器、dry-run、审计与回滚治理边界。
    /// </remarks>
    public interface IProviderAwareExecutionPlanRegressionProbe : IExecutionPlanRegressionProbe {
        PlanRegressionSnapshot Evaluate(in ExecutionPlanProbeRequest request);
    }

    /// <summary>执行计划回退快照（默认 unavailable）。</summary>
    public sealed record PlanRegressionSnapshot(
        bool IsAvailable,
        bool IsRegressed,
        string Summary,
        string UnavailableReason);

    /// <summary>自动验证结构化结果构造器。</summary>
    public static class AutoTuningVerificationResultBuilder {
        public static AutoTuningVerificationResult Build(
            bool regressed,
            bool severeRegressed,
            string reason,
            decimal p95IncreasePercent,
            decimal p99IncreasePercent,
            decimal errorRateIncreasePercent,
            decimal timeoutRateIncreasePercent,
            int deadlockIncreaseCount,
            int? lockWaitBaseline,
            int? lockWaitCurrent,
            PlanRegressionSnapshot planRegression,
            bool lockWaitUnavailable,
            AutoTuningUnavailableReason lockWaitUnavailableReason) {
            var verdict = BuildVerdict(regressed, severeRegressed);
            var lockWaitStatus = lockWaitUnavailable ? "unavailable" : "available";
            var lockWaitReason = lockWaitUnavailable ? lockWaitUnavailableReason.ToTagValue() : AutoTuningUnavailableReason.Sampled.ToTagValue();
            var planStatus = planRegression.IsAvailable ? (planRegression.IsRegressed ? "regressed" : "pass") : "unavailable";
            var planReason = planRegression.IsAvailable ? "probe-sampled" : planRegression.UnavailableReason;
            return new AutoTuningVerificationResult(
                Verdict: verdict,
                Reason: reason,
                SnapshotDiff: [
                    BuildPercentMetric("p95", p95IncreasePercent),
                    BuildPercentMetric("p99", p99IncreasePercent),
                    BuildPercentMetric("error-rate", errorRateIncreasePercent),
                    BuildPercentMetric("timeout-rate", timeoutRateIncreasePercent),
                    new AutoTuningVerificationMetricDiff(
                        Name: "deadlock",
                        Status: deadlockIncreaseCount > 0 ? "regressed" : "pass",
                        Baseline: "0",
                        Current: deadlockIncreaseCount.ToString(),
                        Delta: deadlockIncreaseCount.ToString(),
                        Reason: deadlockIncreaseCount > 0 ? "deadlock increased" : "stable"),
                    new AutoTuningVerificationMetricDiff(
                        Name: "lock-wait",
                        Status: lockWaitStatus,
                        Baseline: lockWaitBaseline?.ToString() ?? "unavailable",
                        Current: lockWaitCurrent?.ToString() ?? "unavailable",
                        Delta: CalculateLockWaitDelta(lockWaitBaseline, lockWaitCurrent, lockWaitUnavailable),
                        Reason: lockWaitReason),
                    new AutoTuningVerificationMetricDiff(
                        Name: "plan-regression",
                        Status: planStatus,
                        Baseline: "n/a",
                        Current: planRegression.Summary,
                        Delta: planRegression.IsRegressed ? "regressed" : "stable",
                        Reason: planReason)
                ]);
        }

        /// <summary>
        /// 执行逻辑：BuildVerdict。
        /// </summary>
        private static string BuildVerdict(bool regressed, bool severeRegressed) {
            if (severeRegressed) {
                return "severe-regressed";
            }

            if (regressed) {
                return "regressed";
            }

            return "pass";
        }

        /// <summary>
        /// 执行逻辑：CalculateLockWaitDelta。
        /// </summary>
        private static string CalculateLockWaitDelta(int? lockWaitBaseline, int? lockWaitCurrent, bool lockWaitUnavailable) {
            if (lockWaitUnavailable || !lockWaitBaseline.HasValue || !lockWaitCurrent.HasValue) {
                return "unavailable";
            }

            return (lockWaitCurrent.Value - lockWaitBaseline.Value).ToString();
        }

        /// <summary>
        /// 执行逻辑：BuildPercentMetric。
        /// </summary>
        private static AutoTuningVerificationMetricDiff BuildPercentMetric(string name, decimal increasePercent) {
            return new AutoTuningVerificationMetricDiff(
                Name: name,
                Status: increasePercent > 0m ? "regressed" : "pass",
                Baseline: "0%",
                Current: $"{increasePercent:F2}%",
                Delta: $"{increasePercent:F2}%",
                Reason: increasePercent > 0m ? $"{name} increased" : "stable");
        }
    }

    /// <summary>默认执行计划探针：结构化输出 unavailable/available 状态，并发出观测指标。</summary>
    public sealed class LoggingOnlyExecutionPlanRegressionProbe : IProviderAwareExecutionPlanRegressionProbe {
        private readonly ILogger<LoggingOnlyExecutionPlanRegressionProbe> _logger;
        /// <summary>
        /// 字段：_observability。
        /// </summary>
        private readonly IAutoTuningObservability _observability;

        public LoggingOnlyExecutionPlanRegressionProbe(
            ILogger<LoggingOnlyExecutionPlanRegressionProbe> logger,
            IAutoTuningObservability observability) {
            _logger = logger;
            _observability = observability;
        }

        /// <summary>
        /// 执行逻辑：Evaluate。
        /// </summary>
        /// <remarks>
        /// 该入口用于保持既有 <see cref="IExecutionPlanRegressionProbe"/> 调用方兼容；
        /// 新增 provider-aware 约定后，这里作为兼容适配层保留，不改变默认 logging-only 行为。
        /// </remarks>
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
            var request = new ExecutionPlanProbeRequest(providerName, sqlFingerprint);
            return Evaluate(request);
        }

        /// <summary>
        /// 执行逻辑：Evaluate。
        /// </summary>
        /// <param name="request">provider-aware 探针请求。</param>
        /// <returns>探针结果快照。</returns>
        public PlanRegressionSnapshot Evaluate(in ExecutionPlanProbeRequest request) {
            var normalizedProvider = NormalizeParameter(request.ProviderName);
            var normalizedFingerprint = NormalizeParameter(request.SqlFingerprint);
            var snapshot = BuildSnapshot(normalizedProvider, normalizedFingerprint);
            _observability.EmitMetric(
                "autotuning.plan_probe.evaluation",
                1d,
                new Dictionary<string, string> {
                    ["provider"] = normalizedProvider,
                    ["fingerprint"] = normalizedFingerprint,
                    ["available"] = snapshot.IsAvailable ? "true" : "false",
                    ["unavailable_reason"] = snapshot.UnavailableReason
                });
            _observability.EmitEvent(
                "autotuning.plan_probe.result",
                snapshot.IsAvailable ? LogLevel.Information : LogLevel.Warning,
                snapshot.Summary,
                new Dictionary<string, string> {
                    ["provider"] = normalizedProvider,
                    ["fingerprint"] = normalizedFingerprint,
                    ["available"] = snapshot.IsAvailable ? "true" : "false",
                    ["unavailable_reason"] = snapshot.UnavailableReason
                });
            _logger.LogInformation(
                "执行计划回退探针评估：Provider={Provider}, Fingerprint={Fingerprint}, IsAvailable={IsAvailable}, IsRegressed={IsRegressed}, UnavailableReason={UnavailableReason}, Summary={Summary}",
                normalizedProvider,
                normalizedFingerprint,
                snapshot.IsAvailable,
                snapshot.IsRegressed,
                snapshot.UnavailableReason,
                snapshot.Summary);
            return snapshot;
        }

        /// <summary>
        /// 执行逻辑：NormalizeParameter。
        /// </summary>
        private static string NormalizeParameter(string? value) {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        /// <summary>
        /// 执行逻辑：BuildSnapshot。
        /// </summary>
        private static PlanRegressionSnapshot BuildSnapshot(string providerName, string sqlFingerprint) {
            if (string.Equals(sqlFingerprint, "plan-probe-query-failed", StringComparison.OrdinalIgnoreCase)) {
                return new PlanRegressionSnapshot(
                    IsAvailable: false,
                    IsRegressed: false,
                    Summary: $"fingerprint={sqlFingerprint}, plan regression unavailable(query failed)",
                    UnavailableReason: AutoTuningUnavailableReason.QueryFailed.ToTagValue());
            }

            if (string.Equals(sqlFingerprint, "plan-probe-permission-denied", StringComparison.OrdinalIgnoreCase)) {
                return new PlanRegressionSnapshot(
                    IsAvailable: false,
                    IsRegressed: false,
                    Summary: $"fingerprint={sqlFingerprint}, plan regression unavailable(permission denied)",
                    UnavailableReason: AutoTuningUnavailableReason.PermissionDenied.ToTagValue());
            }

            if (string.Equals(sqlFingerprint, "plan-probe-available-regressed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sqlFingerprint, "plan-probe-available-pass", StringComparison.OrdinalIgnoreCase)) {
                var regressed = string.Equals(sqlFingerprint, "plan-probe-available-regressed", StringComparison.OrdinalIgnoreCase);
                return new PlanRegressionSnapshot(
                    IsAvailable: true,
                    IsRegressed: regressed,
                    Summary: $"fingerprint={sqlFingerprint}, provider={providerName}, simulated plan regression sampled",
                    UnavailableReason: AutoTuningUnavailableReason.None.ToTagValue());
            }

            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={sqlFingerprint}, provider={providerName}, plan regression unavailable(dialect not supported)",
                UnavailableReason: AutoTuningUnavailableReason.DialectNotSupported.ToTagValue());
        }
    }
}
