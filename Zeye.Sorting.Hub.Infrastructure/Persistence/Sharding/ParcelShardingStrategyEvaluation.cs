using EFCore.Sharding;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel 分表策略配置评估结果。
/// </summary>
/// <param name="Decision">策略决策。</param>
/// <param name="ValidationErrors">结构化配置校验错误。</param>
public readonly record struct ParcelShardingStrategyEvaluation(
    ParcelShardingStrategyDecision Decision,
    IReadOnlyList<string> ValidationErrors);
