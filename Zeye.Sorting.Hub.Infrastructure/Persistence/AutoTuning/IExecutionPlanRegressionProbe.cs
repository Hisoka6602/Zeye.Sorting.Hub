namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 执行计划回退检测抽象（后续可接数据库计划视图）。
/// </summary>
public interface IExecutionPlanRegressionProbe {
    PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint);
}
