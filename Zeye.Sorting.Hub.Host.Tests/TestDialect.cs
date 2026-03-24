using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using System.Data.Common;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 通用数据库方言测试桩，仅用于提供可控 ProviderName。
/// </summary>
internal sealed class TestDialect : IDatabaseDialect, IBatchShardingPhysicalTableProbe {
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

    /// <summary>
    /// 固定返回 true，保持探测调用可通过。
    /// </summary>
    public Task<bool> ExistsAsync(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        string? schemaName,
        string physicalTableName,
        CancellationToken cancellationToken) {
        return Task.FromResult(true);
    }

    /// <summary>
    /// 固定返回空集合，保持测试聚焦。
    /// </summary>
    public Task<IReadOnlyList<string>> FindMissingIndexesAsync(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        string? schemaName,
        string physicalTableName,
        IReadOnlyList<string> indexNames,
        CancellationToken cancellationToken) {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <summary>
    /// 固定返回空集合，保持测试聚焦。
    /// </summary>
    public Task<IReadOnlyList<string>> FindMissingTablesAsync(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        string? schemaName,
        IReadOnlyList<string> physicalTableNames,
        CancellationToken cancellationToken) {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <summary>
    /// 返回固定按日物理表列表，支撑保留候选边界测试。
    /// </summary>
    public Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(
        Microsoft.EntityFrameworkCore.DbContext dbContext,
        string? schemaName,
        string baseTableName,
        CancellationToken cancellationToken) {
        var tables = new[] {
            $"{baseTableName}_20260301",
            $"{baseTableName}_20260302",
            $"{baseTableName}_20260303",
            $"{baseTableName}_20260304"
        };
        return Task.FromResult<IReadOnlyList<string>>(tables);
    }
}
