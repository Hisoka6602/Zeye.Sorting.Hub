using EFCore.Sharding;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.DependencyInjection;

/// <summary>
/// Parcel 聚合分表规则描述（统一声明式定义）。
/// </summary>
/// <param name="EntityType">规则对应实体类型。</param>
/// <param name="RuleKind">规则类别。</param>
/// <param name="Register">规则注册动作。</param>
internal readonly record struct ParcelAggregateShardingRule(
    Type EntityType,
    ParcelAggregateShardingRuleKind RuleKind,
    Action<IShardingBuilder, DateTime, int, ExpandByDateMode> Register);
