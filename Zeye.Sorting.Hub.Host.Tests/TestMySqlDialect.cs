using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 模拟 MySQL ProviderName 的方言测试桩。
/// </summary>
internal sealed class TestMySqlDialect : IDatabaseDialect {
    /// <summary>
    /// 返回 MySQL ProviderName 以命中“仅审计项”索引分支。
    /// </summary>
    public string ProviderName => "MySQL";

    /// <summary>
    /// 返回空集合，确保 MySQL 方言桩仅用于“仅审计项”索引分支识别。
    /// </summary>
    public IReadOnlyList<string> GetOptionalBootstrapSql() => [];

    /// <summary>
    /// 返回空集合，避免自动调优分支影响当前测试。
    /// </summary>
    public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => [];

    /// <summary>
    /// 固定返回 false，保持异常可观测。
    /// </summary>
    public bool ShouldIgnoreAutoTuningException(Exception exception) => false;

    /// <summary>
    /// 返回空集合，避免自治维护 SQL 干扰“仅审计项”行为断言。
    /// </summary>
    public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => [];
}
