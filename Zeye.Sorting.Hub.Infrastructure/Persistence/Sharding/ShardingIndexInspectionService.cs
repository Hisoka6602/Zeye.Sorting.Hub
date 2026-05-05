using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表关键索引巡检服务。
/// </summary>
public sealed class ShardingIndexInspectionService {
    /// <summary>
    /// 分表物理对象探测器。
    /// </summary>
    private readonly IShardingPhysicalTableProbe _physicalTableProbe;

    /// <summary>
    /// 数据库方言。
    /// </summary>
    private readonly IDatabaseDialect _databaseDialect;

    /// <summary>
    /// 初始化分表关键索引巡检服务。
    /// </summary>
    /// <param name="physicalTableProbe">分表物理对象探测器。</param>
    /// <param name="databaseDialect">数据库方言。</param>
    public ShardingIndexInspectionService(
        IShardingPhysicalTableProbe physicalTableProbe,
        IDatabaseDialect databaseDialect) {
        _physicalTableProbe = physicalTableProbe;
        _databaseDialect = databaseDialect;
    }

    /// <summary>
    /// 检查物理表关键索引缺失情况。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="schemaName">schema 名称。</param>
    /// <param name="physicalTableNames">物理表名集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缺失索引描述集合。</returns>
    public async Task<IReadOnlyList<string>> FindMissingIndexesAsync(
        DbContext dbContext,
        string? schemaName,
        IReadOnlyList<string> physicalTableNames,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(physicalTableNames);
        var missingIndexDescriptions = new List<string>();
        foreach (var physicalTableName in physicalTableNames) {
            var expectedIndexes = ResolveExpectedIndexes(physicalTableName);
            if (expectedIndexes.Count == 0) {
                continue;
            }

            var missingIndexes = await _physicalTableProbe.FindMissingIndexesAsync(
                dbContext,
                schemaName,
                physicalTableName,
                expectedIndexes,
                cancellationToken);
            if (missingIndexes.Count == 0) {
                continue;
            }

            missingIndexDescriptions.Add($"{physicalTableName}: {string.Join(", ", missingIndexes)}");
        }

        return missingIndexDescriptions;
    }

    /// <summary>
    /// 根据物理表名解析期望存在的关键索引集合。
    /// </summary>
    /// <param name="physicalTableName">物理表名。</param>
    /// <returns>索引名集合。</returns>
    private IReadOnlyList<string> ResolveExpectedIndexes(string physicalTableName) {
        if (physicalTableName.StartsWith("Parcels_", StringComparison.Ordinal)) {
            return [
                ParcelIndexNames.BagCodeScannedTime,
                ParcelIndexNames.ActualChuteIdScannedTime,
                ParcelIndexNames.TargetChuteIdScannedTime
            ];
        }

        if (physicalTableName.StartsWith("WebRequestAuditLogs_", StringComparison.Ordinal)) {
            return [
                WebRequestAuditLogIndexNames.StartedAt,
                WebRequestAuditLogIndexNames.StatusCodeStartedAt,
                WebRequestAuditLogIndexNames.IsSuccessStartedAt
            ];
        }

        if (physicalTableName.StartsWith("WebRequestAuditLogDetails_", StringComparison.Ordinal)) {
            return [WebRequestAuditLogIndexNames.DetailStartedAt];
        }

        return Array.Empty<string>();
    }
}
