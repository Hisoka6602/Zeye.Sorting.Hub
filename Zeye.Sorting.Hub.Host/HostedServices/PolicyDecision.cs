namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 自动执行策略决策结果。
/// </summary>
internal sealed record PolicyDecision(
    bool ShouldExecute,
    decimal RiskScore,
    string Reason) {
    /// <summary>
    /// 构建“执行”决策。
    /// </summary>
    public static PolicyDecision Execute(decimal riskScore, string reason) => new(true, riskScore, reason);

    /// <summary>
    /// 构建“跳过”决策。
    /// </summary>
    public static PolicyDecision Skip(decimal riskScore, string reason) => new(false, riskScore, reason);
}
