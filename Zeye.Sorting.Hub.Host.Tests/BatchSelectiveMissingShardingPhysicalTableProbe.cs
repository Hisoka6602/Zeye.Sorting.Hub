using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 批量物理表探测测试桩：对指定集合返回缺失并记录批量调用轨迹。
/// </summary>
internal sealed class BatchSelectiveMissingShardingPhysicalTableProbe : IBatchShardingPhysicalTableProbe {
    /// <summary>
    /// 存放需要模拟为缺失的物理表名集合，用于验证批量探测结果与守卫告警拼装。
    /// </summary>
    private readonly IReadOnlySet<string> _missingPhysicalTables;

    /// <summary>
    /// 收集 FindMissingTablesAsync 调用次数，用于断言批量探测路径被命中。
    /// </summary>
    public int FindMissingCallCount { get; private set; }

    /// <summary>
    /// 收集最近一次批量探测入参中的 schemaName，用于断言 schema 透传语义。
    /// </summary>
    public string? LastSchemaName { get; private set; }

    /// <summary>
    /// 初始化批量探测桩。
    /// </summary>
    /// <param name="missingPhysicalTables">缺失表名集合。</param>
    public BatchSelectiveMissingShardingPhysicalTableProbe(IEnumerable<string> missingPhysicalTables) {
        _missingPhysicalTables = new HashSet<string>(missingPhysicalTables, StringComparer.Ordinal);
    }

    /// <summary>
    /// 验证场景：ExistsAsync。
    /// </summary>
    public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
        return Task.FromResult(!_missingPhysicalTables.Contains(physicalTableName));
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
        FindMissingCallCount++;
        LastSchemaName = schemaName;
        var missing = physicalTableNames
            .Where(tableName => _missingPhysicalTables.Contains(tableName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(missing);
    }
}
