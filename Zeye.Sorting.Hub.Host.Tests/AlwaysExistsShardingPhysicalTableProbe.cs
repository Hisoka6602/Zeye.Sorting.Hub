using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 物理表探测测试桩：始终返回存在。
/// </summary>
internal sealed class AlwaysExistsShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
    /// <summary>
    /// 收集 ExistsAsync 被调用的次数，用于断言守卫流程确实执行了物理表探测。
    /// </summary>
    public int CallCount { get; private set; }

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
}
