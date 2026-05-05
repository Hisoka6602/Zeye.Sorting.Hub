using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 可配置分表物理对象探测测试桩。
/// </summary>
internal sealed class ConfigurableShardingPhysicalTableProbe : IBatchShardingPhysicalTableProbe {
    /// <summary>
    /// 缺失表名集合；为空且 AllRequestedTablesMissing 为 false 时表示无缺失。
    /// </summary>
    private readonly IReadOnlySet<string> _missingTables;

    /// <summary>
    /// 缺失索引映射。
    /// </summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _missingIndexesByTable;

    /// <summary>
    /// 初始化可配置分表物理对象探测测试桩。
    /// </summary>
    /// <param name="missingTables">缺失表名集合。</param>
    /// <param name="missingIndexesByTable">缺失索引映射。</param>
    /// <param name="allRequestedTablesMissing">是否将所有请求表都视为缺失。</param>
    public ConfigurableShardingPhysicalTableProbe(
        IEnumerable<string>? missingTables = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? missingIndexesByTable = null,
        bool allRequestedTablesMissing = false) {
        _missingTables = new HashSet<string>(missingTables ?? [], StringComparer.Ordinal);
        _missingIndexesByTable = missingIndexesByTable ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        AllRequestedTablesMissing = allRequestedTablesMissing;
    }

    /// <summary>
    /// 是否将所有请求表都视为缺失。
    /// </summary>
    public bool AllRequestedTablesMissing { get; }

    /// <summary>
    /// 批量缺表探测调用次数。
    /// </summary>
    public int FindMissingTablesCallCount { get; private set; }

    /// <summary>
    /// 最近一次请求表名集合。
    /// </summary>
    public IReadOnlyList<string> LastRequestedTables { get; private set; } = [];

    /// <inheritdoc />
    public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
        return Task.FromResult(!_missingTables.Contains(physicalTableName));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindMissingIndexesAsync(
        DbContext dbContext,
        string? schemaName,
        string physicalTableName,
        IReadOnlyList<string> indexNames,
        CancellationToken cancellationToken) {
        if (!_missingIndexesByTable.TryGetValue(physicalTableName, out var missingIndexes)) {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var missingSet = missingIndexes.ToHashSet(StringComparer.Ordinal);
        var result = indexNames.Where(indexName => missingSet.Contains(indexName)).ToArray();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> FindMissingTablesAsync(
        DbContext dbContext,
        string? schemaName,
        IReadOnlyList<string> physicalTableNames,
        CancellationToken cancellationToken) {
        FindMissingTablesCallCount++;
        LastRequestedTables = physicalTableNames.ToArray();
        var missing = AllRequestedTablesMissing
            ? physicalTableNames.ToArray()
            : physicalTableNames.Where(tableName => _missingTables.Contains(tableName)).ToArray();
        return Task.FromResult<IReadOnlyList<string>>(missing);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(
        DbContext dbContext,
        string? schemaName,
        string baseTableName,
        CancellationToken cancellationToken) {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
