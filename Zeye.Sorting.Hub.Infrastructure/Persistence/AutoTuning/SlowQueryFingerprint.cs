namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询指纹模型。
/// </summary>
/// <param name="Fingerprint">指纹标识。</param>
/// <param name="NormalizedSql">去参数化后的标准 SQL。</param>
public sealed record SlowQueryFingerprint(
    string Fingerprint,
    string NormalizedSql);
