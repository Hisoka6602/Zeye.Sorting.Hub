namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// provider-aware 执行计划回退探针扩展请求（为未来真实 EXPLAIN/SHOWPLAN 实现预留上下文）。
/// </summary>
/// <param name="ProviderName">数据库提供器名称。</param>
/// <param name="SqlFingerprint">标准化 SQL 指纹。</param>
public readonly record struct ExecutionPlanProbeRequest(
    string ProviderName,
    string SqlFingerprint);
