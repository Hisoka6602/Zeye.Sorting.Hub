namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

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
