namespace Zeye.Sorting.Hub.SharedKernel.Utilities;

/// <summary>
/// 配置变更历史记录条目，保存单次配置变更的快照信息。
/// </summary>
/// <typeparam name="T">配置类型。</typeparam>
/// <param name="Sequence">变更序号（单调递增）。</param>
/// <param name="PreviousValue">变更前配置值（首次记录时为 null）。</param>
/// <param name="CurrentValue">变更后配置值。</param>
/// <param name="EffectiveTime">变更生效本地时间（Kind 应为 Local，禁止传入 UTC 时间；全局禁止 UTC 语义）。</param>
/// <param name="ChangedFields">变更字段摘要描述（由外部调用方提供）。</param>
public sealed record ConfigChangeEntry<T>(
    int Sequence,
    T? PreviousValue,
    T CurrentValue,
    DateTime EffectiveTime,
    string ChangedFields) where T : class;
