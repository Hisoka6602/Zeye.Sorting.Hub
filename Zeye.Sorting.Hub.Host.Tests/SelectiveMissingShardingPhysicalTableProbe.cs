using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 物理表探测测试桩：仅对指定表返回缺失。
/// </summary>
internal sealed class SelectiveMissingShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
    /// <summary>
    /// 存放需要模拟为缺失的物理表名集合，用于构造“部分分表未预建”断言场景。
    /// </summary>
    private readonly IReadOnlySet<string> _missingPhysicalTables;

    /// <summary>
    /// 收集 ExistsAsync 被调用次数，用于断言守卫路径中的探测频率。
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 初始化选择性缺失探测器。
    /// </summary>
    /// <param name="missingPhysicalTables">缺失表名集合。</param>
    public SelectiveMissingShardingPhysicalTableProbe(IEnumerable<string> missingPhysicalTables) {
        _missingPhysicalTables = new HashSet<string>(missingPhysicalTables, StringComparer.Ordinal);
    }

    /// <summary>
    /// 验证场景：ExistsAsync。
    /// </summary>
    public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
        CallCount++;
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
}
