using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 索引探测测试桩：按表名返回预设缺失索引集合。
/// </summary>
internal sealed class MissingIndexShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
    /// <summary>
    /// 存放“物理表 -> 缺失索引列表”映射，用于断言关键索引审计结果。
    /// </summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _missingIndexesByTable;

    /// <summary>
    /// 初始化缺失索引探测桩。
    /// </summary>
    /// <param name="missingIndexesByTable">缺失索引映射（Key=物理表名，Value=缺失索引名）。</param>
    public MissingIndexShardingPhysicalTableProbe(IReadOnlyDictionary<string, IReadOnlyList<string>> missingIndexesByTable) {
        _missingIndexesByTable = missingIndexesByTable;
    }

    /// <summary>
    /// 验证场景：ExistsAsync。
    /// </summary>
    public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
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
        if (!_missingIndexesByTable.TryGetValue(physicalTableName, out var missingIndexes)) {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var missingSet = missingIndexes.ToHashSet(StringComparer.Ordinal);
        var result = indexNames
            .Where(indexName => missingSet.Contains(indexName))
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(result);
    }
}
