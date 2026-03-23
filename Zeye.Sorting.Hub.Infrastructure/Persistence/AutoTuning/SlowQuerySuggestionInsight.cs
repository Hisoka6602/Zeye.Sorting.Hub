namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询建议洞察。
/// </summary>
public sealed record SlowQuerySuggestionInsight(
    string SqlFingerprint,
    string SuggestionSql,
    string Reason,
    string RiskLevel,
    decimal Confidence);
