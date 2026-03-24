using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using System.Data.Common;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 模拟 SQL Server ProviderName 的方言测试桩，用于验证 batch probe 的 schema 透传语义。
/// </summary>
internal sealed class TestSqlServerDialect : IDatabaseDialect {
    /// <summary>
    /// 返回 SQL Server ProviderName 以命中 SQL Server 相关分支。
    /// </summary>
    public string ProviderName => "SQLServer";

    /// <summary>
    /// 返回空集合，确保 SQL Server 方言桩仅用于 ProviderName 识别与 schema 透传测试。
    /// </summary>
    public IReadOnlyList<string> GetOptionalBootstrapSql() => [];

    /// <summary>
    /// 返回空集合，避免额外 SQL 影响守卫测试路径。
    /// </summary>
    public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => [];

    /// <summary>
    /// 固定返回 false，保持异常行为可观测。
    /// </summary>
    public bool ShouldIgnoreAutoTuningException(Exception exception) => false;

    /// <summary>
    /// 返回空集合，避免自治维护逻辑干扰 ProviderName 相关断言。
    /// </summary>
    public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => [];

    /// <summary>
    /// 返回固定数据库名，便于测试分支控制。
    /// </summary>
    public string ExtractDatabaseName(string connectionString) => "test_db";

    /// <summary>
    /// 当前测试桩不支持管理连接。
    /// </summary>
    public DbConnection CreateAdministrationConnection(string connectionString) => throw new NotSupportedException();

    /// <summary>
    /// 固定返回已存在，避免触发真实创建链路。
    /// </summary>
    public Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) => Task.FromResult(true);

    /// <summary>
    /// 测试桩无需真实建库。
    /// </summary>
    public Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) => Task.CompletedTask;
}
