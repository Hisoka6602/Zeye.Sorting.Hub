using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表预建计划服务。
/// </summary>
public sealed class ShardingTablePrebuildService {
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
    /// 分表预建配置。
    /// </summary>
    private readonly ShardingPrebuildOptions _options;

    /// <summary>
    /// 最近一次预建计划。
    /// </summary>
    private ShardingPrebuildPlan? _lastPlan;

    /// <summary>
    /// 初始化分表预建计划服务。
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂。</param>
    /// <param name="physicalTableProbe">批量物理表探测器。</param>
    /// <param name="databaseDialect">数据库方言。</param>
    /// <param name="tablePlanBuilder">分表物理表规划构建器。</param>
    /// <param name="options">分表预建配置。</param>
    public ShardingTablePrebuildService(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        IBatchShardingPhysicalTableProbe physicalTableProbe,
        IDatabaseDialect databaseDialect,
        ShardingPhysicalTablePlanBuilder tablePlanBuilder,
        IOptions<ShardingPrebuildOptions> options) {
        _dbContextFactory = dbContextFactory;
        _physicalTableProbe = physicalTableProbe;
        _databaseDialect = databaseDialect;
        _tablePlanBuilder = tablePlanBuilder;
        _options = options.Value;
    }

    /// <summary>
    /// 获取最近一次预建计划。
    /// </summary>
    /// <returns>预建计划。</returns>
    public ShardingPrebuildPlan? GetLastPlan() {
        return Volatile.Read(ref _lastPlan);
    }

    /// <summary>
    /// 生成分表预建计划。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预建计划。</returns>
    public async Task<ShardingPrebuildPlan> BuildPlanAsync(CancellationToken cancellationToken) {
        if (!_options.IsEnabled) {
            var disabledPlan = BuildPlan([], [], "分表预建计划未启用。", false);
            Volatile.Write(ref _lastPlan, disabledPlan);
            return disabledPlan;
        }

        try {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var schemaName = ResolveProbeSchemaName(_databaseDialect.ProviderName);
            var plannedTableNames = _tablePlanBuilder.BuildExpectedPhysicalTableNames(
                dbContext,
                DateTime.Now,
                _options.PrebuildAheadHours,
                true);
            var missingTables = await _physicalTableProbe.FindMissingTablesAsync(
                dbContext,
                schemaName,
                plannedTableNames,
                cancellationToken);
            var plan = BuildPlan(
                plannedTableNames,
                missingTables,
                _options.DryRun ? "分表预建 dry-run 计划已生成；未执行任何 DDL。" : "分表预建计划已生成；真实执行入口待危险动作隔离器放行。",
                true);
            Volatile.Write(ref _lastPlan, plan);
            Logger.Info(
                "分表预建计划生成：Provider={Provider}, DryRun={DryRun}, PrebuildAheadHours={PrebuildAheadHours}, PlannedCount={PlannedCount}, MissingCount={MissingCount}",
                _databaseDialect.ProviderName,
                plan.IsDryRun,
                plan.PrebuildAheadHours,
                plan.PlannedPhysicalTables.Count,
                plan.MissingPhysicalTables.Count);
            return plan;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Warn("分表预建计划生成收到取消信号。");
            throw;
        }
        catch (Exception ex) {
            Logger.Error(ex, "分表预建计划生成失败。");
            var failedPlan = BuildPlan([], [], $"分表预建计划生成失败：{ex.Message}", true);
            Volatile.Write(ref _lastPlan, failedPlan);
            return failedPlan;
        }
    }

    /// <summary>
    /// 构建预建计划对象。
    /// </summary>
    /// <param name="plannedPhysicalTables">计划物理表集合。</param>
    /// <param name="missingPhysicalTables">缺失物理表集合。</param>
    /// <param name="message">摘要消息。</param>
    /// <param name="isEnabled">是否启用。</param>
    /// <returns>预建计划。</returns>
    private ShardingPrebuildPlan BuildPlan(
        IReadOnlyList<string> plannedPhysicalTables,
        IReadOnlyList<string> missingPhysicalTables,
        string message,
        bool isEnabled) {
        return new ShardingPrebuildPlan {
            GeneratedAtLocal = DateTime.Now,
            IsEnabled = isEnabled,
            IsDryRun = _options.DryRun,
            PrebuildAheadHours = _options.PrebuildAheadHours,
            PlannedPhysicalTables = plannedPhysicalTables,
            MissingPhysicalTables = missingPhysicalTables,
            Message = message
        };
    }

    /// <summary>
    /// 解析物理表探测 schema 名称。
    /// </summary>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <returns>schema 名称。</returns>
    private static string? ResolveProbeSchemaName(string providerName) {
        return string.Equals(providerName, "SQLServer", StringComparison.OrdinalIgnoreCase) ? "dbo" : null;
    }
}
