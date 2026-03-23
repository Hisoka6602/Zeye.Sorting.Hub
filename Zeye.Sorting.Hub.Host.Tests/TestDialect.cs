using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 通用数据库方言测试桩，仅用于提供可控 ProviderName。
/// </summary>
internal sealed class TestDialect : IDatabaseDialect {
    /// <summary>
    /// 返回测试方言名称，用于触发默认治理分支。
    /// </summary>
    public string ProviderName => "Test";

    /// <summary>
    /// 返回空集合，确保测试只关注 ProviderName 对治理分支的影响，不引入方言初始化 SQL 干扰。
    /// </summary>
    public IReadOnlyList<string> GetOptionalBootstrapSql() => [];

    /// <summary>
    /// 返回空集合，避免自动调优 SQL 参与测试断言，保持用例聚焦于守卫与审计路径。
    /// </summary>
    public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => [];

    /// <summary>
    /// 固定返回 false，避免异常分支吞掉测试期异常，确保断言可观察真实错误。
    /// </summary>
    public bool ShouldIgnoreAutoTuningException(Exception exception) => false;

    /// <summary>
    /// 返回空集合，避免自治维护 SQL 干扰当前测试目标（仅验证治理守卫与 Provider 分支）。
    /// </summary>
    public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => [];
}
