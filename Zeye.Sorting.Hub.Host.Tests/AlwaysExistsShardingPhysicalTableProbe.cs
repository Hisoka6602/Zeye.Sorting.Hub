using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 物理表探测测试桩：始终返回存在。
/// </summary>
internal sealed class AlwaysExistsShardingPhysicalTableProbe : IBatchShardingPhysicalTableProbe {
    /// <summary>
    /// 收集 ExistsAsync 被调用的次数，用于断言守卫流程确实执行了物理表探测。
    /// </summary>
    public int CallCount { get; private set; }
    /// <summary>
    /// 收集 FindMissingTablesAsync 被调用次数。
    /// </summary>
    public int FindMissingTablesCallCount { get; private set; }
    /// <summary>
    /// 收集 ListPhysicalTablesByBaseNameAsync 被调用次数。
    /// </summary>
    public int ListPhysicalTablesCallCount { get; private set; }

    /// <summary>
    /// 验证场景：ExistsAsync。
    /// </summary>
    public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
        CallCount++;
        return Task.FromResult(true);
    }

    /// <summary>
    /// 验证场景：FindMissingIndexesAsync。
    /// </summary>
    public Task<IReadOnlyList<string>> FindMissingIndexesAsync(
        DbContext dbContext,
        string? schemaName,
        string physicalTableName,
        IReadOnlyList<string> indexNames,
        CancellationToken cancellationToken) {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <summary>
    /// 验证场景：FindMissingTablesAsync。
    /// </summary>
    public Task<IReadOnlyList<string>> FindMissingTablesAsync(
        DbContext dbContext,
        string? schemaName,
        IReadOnlyList<string> physicalTableNames,
        CancellationToken cancellationToken) {
        FindMissingTablesCallCount++;
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <summary>
    /// 验证场景：ListPhysicalTablesByBaseNameAsync。
    /// </summary>
    public Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(
        DbContext dbContext,
        string? schemaName,
        string baseTableName,
        CancellationToken cancellationToken) {
        ListPhysicalTablesCallCount++;
        var tables = new[] {
            $"{baseTableName}_20260301",
            $"{baseTableName}_20260302",
            $"{baseTableName}_20260303",
            $"{baseTableName}_20260304"
        };
        return Task.FromResult<IReadOnlyList<string>>(tables);
    }
}
