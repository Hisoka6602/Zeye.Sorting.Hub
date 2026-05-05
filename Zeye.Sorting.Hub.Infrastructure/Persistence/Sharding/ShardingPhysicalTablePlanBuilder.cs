using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表物理表规划构建器。
/// </summary>
public sealed class ShardingPhysicalTablePlanBuilder {
    /// <summary>
    /// Parcel 分表策略决策。
    /// </summary>
    private readonly ParcelShardingStrategyDecision _parcelShardingStrategyDecision;

    /// <summary>
    /// 初始化分表物理表规划构建器。
    /// </summary>
    /// <param name="parcelShardingStrategyDecision">Parcel 分表策略决策。</param>
    public ShardingPhysicalTablePlanBuilder(ParcelShardingStrategyDecision parcelShardingStrategyDecision) {
        _parcelShardingStrategyDecision = parcelShardingStrategyDecision;
    }

    /// <summary>
    /// 构建当前与后续窗口内需要关注的物理分表名。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="startAtLocal">规划开始时间（本地时间）。</param>
    /// <param name="aheadHours">向前规划小时数。</param>
    /// <param name="shouldIncludeNextPeriod">是否至少包含下一周期。</param>
    /// <returns>物理分表名集合。</returns>
    public IReadOnlyList<string> BuildExpectedPhysicalTableNames(
        SortingHubDbContext dbContext,
        DateTime startAtLocal,
        int aheadHours,
        bool shouldIncludeNextPeriod) {
        ArgumentNullException.ThrowIfNull(dbContext);
        var normalizedStartAtLocal = DateTime.SpecifyKind(startAtLocal, DateTimeKind.Local);
        var normalizedAheadHours = Math.Max(0, aheadHours);
        var endAtLocal = normalizedStartAtLocal.AddHours(normalizedAheadHours);
        var tableNames = new HashSet<string>(StringComparer.Ordinal);

        // 步骤 1：Parcel 主表与日期型值对象按当前策略决策的粒度生成巡检目标。
        var parcelBaseTableNames = ResolveBaseTableNames(
            dbContext,
            PersistenceServiceCollectionExtensions.GetParcelPerDayShardingEntityTypes());
        var parcelDateMode = _parcelShardingStrategyDecision.EffectiveDateMode;
        AddPhysicalTables(tableNames, parcelBaseTableNames, parcelDateMode, normalizedStartAtLocal, endAtLocal, shouldIncludeNextPeriod);

        // 步骤 2：WebRequestAuditLog 热表与详情表固定按日治理。
        var auditBaseTableNames = ResolveBaseTableNames(
            dbContext,
            [typeof(WebRequestAuditLog), typeof(WebRequestAuditLogDetail)]);
        AddPhysicalTables(tableNames, auditBaseTableNames, ExpandByDateMode.PerDay, normalizedStartAtLocal, endAtLocal, shouldIncludeNextPeriod);

        return tableNames.OrderBy(static tableName => tableName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// 从 EF 模型解析实体基础表名。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="entityTypes">实体类型集合。</param>
    /// <returns>基础表名集合。</returns>
    private static IReadOnlyList<string> ResolveBaseTableNames(SortingHubDbContext dbContext, IReadOnlyList<Type> entityTypes) {
        var baseTableNames = new List<string>(entityTypes.Count);
        foreach (var entityType in entityTypes) {
            var tableName = dbContext.Model
                .GetEntityTypes()
                .Where(modelEntityType => modelEntityType.ClrType == entityType)
                .Select(static modelEntityType => modelEntityType.GetTableName())
                .Where(static mappedTableName => !string.IsNullOrWhiteSpace(mappedTableName))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .SingleOrDefault();
            if (!string.IsNullOrWhiteSpace(tableName)) {
                baseTableNames.Add(tableName);
            }
        }

        return baseTableNames.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// 按日期粒度添加物理表名。
    /// </summary>
    /// <param name="physicalTableNames">物理表名集合。</param>
    /// <param name="baseTableNames">基础表名集合。</param>
    /// <param name="dateMode">日期分表模式。</param>
    /// <param name="startAtLocal">开始时间。</param>
    /// <param name="endAtLocal">结束时间。</param>
    /// <param name="shouldIncludeNextPeriod">是否包含下一周期。</param>
    private static void AddPhysicalTables(
        HashSet<string> physicalTableNames,
        IReadOnlyList<string> baseTableNames,
        ExpandByDateMode dateMode,
        DateTime startAtLocal,
        DateTime endAtLocal,
        bool shouldIncludeNextPeriod) {
        foreach (var suffix in BuildDateSuffixes(dateMode, startAtLocal, endAtLocal, shouldIncludeNextPeriod)) {
            foreach (var baseTableName in baseTableNames) {
                physicalTableNames.Add($"{baseTableName}_{suffix}");
            }
        }
    }

    /// <summary>
    /// 按分表日期模式构建后缀集合。
    /// </summary>
    /// <param name="dateMode">日期分表模式。</param>
    /// <param name="startAtLocal">开始时间。</param>
    /// <param name="endAtLocal">结束时间。</param>
    /// <param name="shouldIncludeNextPeriod">是否包含下一周期。</param>
    /// <returns>后缀集合。</returns>
    private static IReadOnlyList<string> BuildDateSuffixes(
        ExpandByDateMode dateMode,
        DateTime startAtLocal,
        DateTime endAtLocal,
        bool shouldIncludeNextPeriod) {
        return dateMode switch {
            ExpandByDateMode.PerDay => BuildDailySuffixes(startAtLocal, endAtLocal, shouldIncludeNextPeriod),
            ExpandByDateMode.PerMonth => BuildMonthlySuffixes(startAtLocal, endAtLocal, shouldIncludeNextPeriod),
            _ => BuildDailySuffixes(startAtLocal, endAtLocal, shouldIncludeNextPeriod)
        };
    }

    /// <summary>
    /// 构建按日后缀集合。
    /// </summary>
    /// <param name="startAtLocal">开始时间。</param>
    /// <param name="endAtLocal">结束时间。</param>
    /// <param name="shouldIncludeNextPeriod">是否包含下一天。</param>
    /// <returns>按日后缀集合。</returns>
    private static IReadOnlyList<string> BuildDailySuffixes(DateTime startAtLocal, DateTime endAtLocal, bool shouldIncludeNextPeriod) {
        var endDate = endAtLocal.Date;
        if (shouldIncludeNextPeriod && endDate <= startAtLocal.Date) {
            endDate = startAtLocal.Date.AddDays(1);
        }

        var suffixes = new List<string>();
        for (var date = startAtLocal.Date; date <= endDate; date = date.AddDays(1)) {
            suffixes.Add(date.ToString("yyyyMMdd"));
        }

        return suffixes;
    }

    /// <summary>
    /// 构建按月后缀集合。
    /// </summary>
    /// <param name="startAtLocal">开始时间。</param>
    /// <param name="endAtLocal">结束时间。</param>
    /// <param name="shouldIncludeNextPeriod">是否包含下一月。</param>
    /// <returns>按月后缀集合。</returns>
    private static IReadOnlyList<string> BuildMonthlySuffixes(DateTime startAtLocal, DateTime endAtLocal, bool shouldIncludeNextPeriod) {
        var startMonth = new DateTime(startAtLocal.Year, startAtLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var endMonth = new DateTime(endAtLocal.Year, endAtLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
        if (shouldIncludeNextPeriod && endMonth <= startMonth) {
            endMonth = startMonth.AddMonths(1);
        }

        var suffixes = new List<string>();
        for (var date = startMonth; date <= endMonth; date = date.AddMonths(1)) {
            suffixes.Add(date.ToString("yyyyMM"));
        }

        return suffixes;
    }
}
