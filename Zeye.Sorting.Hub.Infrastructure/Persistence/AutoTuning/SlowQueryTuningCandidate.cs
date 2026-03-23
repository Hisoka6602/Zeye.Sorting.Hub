namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询调优候选。
/// </summary>
public sealed record SlowQueryTuningCandidate(
    string SqlFingerprint,
    string? SchemaName,
    string TableName,
    IReadOnlyList<string> WhereColumns,
    IReadOnlyList<string> SuggestedActions);
