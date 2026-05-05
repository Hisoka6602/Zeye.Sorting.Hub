using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表物理表巡检服务。
/// </summary>
public sealed class ShardingTableInspectionService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _dbContextFactory;

    /// <summary>
    /// 批量物理表探测器。
    /// </summary>
    private readonly IBatchShardingPhysicalTableProbe _physicalTableProbe;

    /// <summary>
    /// 数据库方言。
    /// </summary>
    private readonly IDatabaseDialect _databaseDialect;

    /// <summary>
    /// 分表物理表规划构建器。
    /// </summary>
    private readonly ShardingPhysicalTablePlanBuilder _tablePlanBuilder;

    /// <summary>
    /// 分表关键索引巡检服务。
    /// </summary>
    private readonly ShardingIndexInspectionService _indexInspectionService;

    /// <summary>
    /// 分表容量风险快照服务。
    /// </summary>
    private readonly ShardingCapacitySnapshotService _capacitySnapshotService;

    /// <summary>
    /// 巡检配置。
    /// </summary>
    private readonly ShardingRuntimeInspectionOptions _options;

    /// <summary>
    /// 最近一次巡检报告。
    /// </summary>
    private ShardingInspectionReport? _lastReport;

    /// <summary>
    /// 初始化分表物理表巡检服务。
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂。</param>
    /// <param name="physicalTableProbe">批量物理表探测器。</param>
    /// <param name="databaseDialect">数据库方言。</param>
    /// <param name="tablePlanBuilder">分表物理表规划构建器。</param>
    /// <param name="indexInspectionService">分表关键索引巡检服务。</param>
    /// <param name="capacitySnapshotService">分表容量风险快照服务。</param>
    /// <param name="options">巡检配置。</param>
    public ShardingTableInspectionService(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        IBatchShardingPhysicalTableProbe physicalTableProbe,
        IDatabaseDialect databaseDialect,
        ShardingPhysicalTablePlanBuilder tablePlanBuilder,
        ShardingIndexInspectionService indexInspectionService,
        ShardingCapacitySnapshotService capacitySnapshotService,
        IOptions<ShardingRuntimeInspectionOptions> options) {
        _dbContextFactory = dbContextFactory;
        _physicalTableProbe = physicalTableProbe;
        _databaseDialect = databaseDialect;
        _tablePlanBuilder = tablePlanBuilder;
        _indexInspectionService = indexInspectionService;
        _capacitySnapshotService = capacitySnapshotService;
        _options = options.Value;
    }

    /// <summary>
    /// 获取最近一次巡检报告。
    /// </summary>
    /// <returns>巡检报告。</returns>
    public ShardingInspectionReport? GetLastReport() {
        return Volatile.Read(ref _lastReport);
    }

    /// <summary>
    /// 执行一次分表巡检。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>巡检报告。</returns>
    public async Task<ShardingInspectionReport> InspectAsync(CancellationToken cancellationToken) {
        if (!_options.IsEnabled) {
            var disabledReport = BuildReport([], [], [], [], true, "分表运行期巡检未启用。", false);
            Volatile.Write(ref _lastReport, disabledReport);
            return disabledReport;
        }

        try {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var schemaName = ResolveProbeSchemaName(_databaseDialect.ProviderName);
            var plannedTableNames = _tablePlanBuilder.BuildExpectedPhysicalTableNames(
                dbContext,
                DateTime.Now,
                0,
                _options.ShouldCheckNextPeriodTables);
            var missingPhysicalTables = await _physicalTableProbe.FindMissingTablesAsync(
                dbContext,
                schemaName,
                plannedTableNames,
                cancellationToken);
            var existingPhysicalTables = plannedTableNames
                .Where(tableName => !missingPhysicalTables.Contains(tableName, StringComparer.Ordinal))
                .ToArray();
            var missingIndexes = _options.ShouldCheckIndexes
                ? await _indexInspectionService.FindMissingIndexesAsync(dbContext, schemaName, existingPhysicalTables, cancellationToken)
                : Array.Empty<string>();
            var capacityWarnings = _options.ShouldCheckCapacity
                ? _capacitySnapshotService.BuildWarnings()
                : Array.Empty<string>();
            var pairWarnings = BuildWebRequestAuditLogPairWarnings(plannedTableNames, missingPhysicalTables);
            var isHealthy = missingPhysicalTables.Count == 0 && missingIndexes.Count == 0 && capacityWarnings.Count == 0 && pairWarnings.Count == 0;
            var report = BuildReport(
                missingPhysicalTables,
                missingIndexes,
                capacityWarnings,
                pairWarnings,
                isHealthy,
                isHealthy ? "分表巡检通过。" : "分表巡检发现风险。",
                true);
            Volatile.Write(ref _lastReport, report);
            LogReport(report);
            return report;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Warn("分表运行期巡检收到取消信号。");
            throw;
        }
        catch (Exception ex) {
            Logger.Error(ex, "分表运行期巡检发生异常。");
            var failedReport = BuildReport([], [], [$"分表巡检异常：{ex.Message}"], [], false, "分表巡检执行失败。", true);
            Volatile.Write(ref _lastReport, failedReport);
            return failedReport;
        }
    }

    /// <summary>
    /// 构建巡检报告。
    /// </summary>
    /// <param name="missingPhysicalTables">缺失物理表集合。</param>
    /// <param name="missingIndexes">缺失索引集合。</param>
    /// <param name="capacityWarnings">容量风险集合。</param>
    /// <param name="pairWarnings">热表与详情表一致性风险集合。</param>
    /// <param name="isHealthy">是否健康。</param>
    /// <param name="message">摘要消息。</param>
    /// <param name="isEnabled">是否启用。</param>
    /// <returns>巡检报告。</returns>
    private ShardingInspectionReport BuildReport(
        IReadOnlyList<string> missingPhysicalTables,
        IReadOnlyList<string> missingIndexes,
        IReadOnlyList<string> capacityWarnings,
        IReadOnlyList<string> pairWarnings,
        bool isHealthy,
        string message,
        bool isEnabled) {
        return new ShardingInspectionReport {
            CheckedAtLocal = DateTime.Now,
            ProviderName = _databaseDialect.ProviderName,
            IsEnabled = isEnabled,
            MissingPhysicalTables = missingPhysicalTables,
            MissingIndexes = missingIndexes,
            CapacityWarnings = capacityWarnings,
            WebRequestAuditLogPairWarnings = pairWarnings,
            IsHealthy = isHealthy,
            Message = message
        };
    }

    /// <summary>
    /// 构建 WebRequestAuditLog 热表与详情表一致性风险。
    /// </summary>
    /// <param name="plannedTableNames">计划物理表集合。</param>
    /// <param name="missingPhysicalTables">缺失物理表集合。</param>
    /// <returns>一致性风险集合。</returns>
    private static IReadOnlyList<string> BuildWebRequestAuditLogPairWarnings(
        IReadOnlyList<string> plannedTableNames,
        IReadOnlyList<string> missingPhysicalTables) {
        var plannedSet = plannedTableNames.ToHashSet(StringComparer.Ordinal);
        var missingSet = missingPhysicalTables.ToHashSet(StringComparer.Ordinal);
        var warnings = new List<string>();
        foreach (var hotTableName in plannedSet.Where(static tableName => tableName.StartsWith("WebRequestAuditLogs_", StringComparison.Ordinal))) {
            var suffix = hotTableName["WebRequestAuditLogs_".Length..];
            var detailTableName = $"WebRequestAuditLogDetails_{suffix}";
            if (!plannedSet.Contains(detailTableName)) {
                continue;
            }

            var isHotMissing = missingSet.Contains(hotTableName);
            var isDetailMissing = missingSet.Contains(detailTableName);
            if (isHotMissing != isDetailMissing) {
                warnings.Add($"WebRequestAuditLog 热表与详情表缺失状态不一致：Hot={hotTableName}, Detail={detailTableName}");
            }
        }

        return warnings;
    }

    /// <summary>
    /// 解析物理表探测 schema 名称。
    /// </summary>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <returns>schema 名称。</returns>
    private static string? ResolveProbeSchemaName(string providerName) {
        return string.Equals(providerName, "SQLServer", StringComparison.OrdinalIgnoreCase) ? "dbo" : null;
    }

    /// <summary>
    /// 记录巡检报告日志。
    /// </summary>
    /// <param name="report">巡检报告。</param>
    private static void LogReport(ShardingInspectionReport report) {
        if (report.IsHealthy) {
            Logger.Info("分表巡检通过：Provider={Provider}, CheckedAtLocal={CheckedAtLocal}", report.ProviderName, report.CheckedAtLocal);
            return;
        }

        Logger.Warn(
            "分表巡检发现风险：Provider={Provider}, MissingTables={MissingTables}, MissingIndexes={MissingIndexes}, CapacityWarnings={CapacityWarnings}, PairWarnings={PairWarnings}",
            report.ProviderName,
            string.Join(", ", report.MissingPhysicalTables),
            string.Join(" | ", report.MissingIndexes),
            string.Join(" | ", report.CapacityWarnings),
            string.Join(" | ", report.WebRequestAuditLogPairWarnings));
    }
}
