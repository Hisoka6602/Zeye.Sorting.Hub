using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

/// <summary>
/// 数据归档 dry-run 计划器。
/// </summary>
public sealed class DataArchivePlanner {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _dbContextFactory;

    /// <summary>
    /// 归档配置。
    /// </summary>
    private readonly DataArchiveOptions _options;

    /// <summary>
    /// 初始化数据归档计划器。
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂。</param>
    /// <param name="options">归档配置。</param>
    public DataArchivePlanner(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        IOptions<DataArchiveOptions> options) {
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
    }

    /// <summary>
    /// 构建归档 dry-run 计划。
    /// </summary>
    /// <param name="archiveTask">归档任务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>计划数量、摘要与检查点载荷。</returns>
    public async Task<(long PlannedItemCount, string PlanSummary, string CheckpointPayload)> BuildPlanAsync(
        ArchiveTask archiveTask,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(archiveTask);
        if (archiveTask.TaskType != ArchiveTaskType.WebRequestAuditLogHistory) {
            throw new NotSupportedException($"暂不支持的归档任务类型：{archiveTask.TaskType}。");
        }

        var cutoffTime = DateTime.Now.AddDays(-archiveTask.RetentionDays);
        try {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var baseQuery = dbContext.Set<WebRequestAuditLog>()
                .AsNoTracking()
                .Where(x => x.StartedAt < cutoffTime);
            var plannedItemCount = await baseQuery.LongCountAsync(cancellationToken);
            var summarySample = await baseQuery
                .OrderBy(x => x.StartedAt)
                .ThenBy(x => x.Id)
                .Take(_options.SampleItemLimit)
                .Select(x => new {
                    x.Id,
                    x.TraceId,
                    x.RequestPath,
                    x.StartedAt,
                    x.StatusCode
                })
                .ToArrayAsync(cancellationToken);
            var range = await baseQuery
                .GroupBy(static _ => 1)
                .Select(group => new {
                    MinStartedAt = group.Min(x => x.StartedAt),
                    MaxStartedAt = group.Max(x => x.StartedAt)
                })
                .FirstOrDefaultAsync(cancellationToken);
            var checkpointObject = new {
                archiveTaskId = archiveTask.Id,
                taskType = archiveTask.TaskType.ToString(),
                retentionDays = archiveTask.RetentionDays,
                generatedAtLocal = DateTime.Now,
                cutoffTimeLocal = cutoffTime,
                plannedItemCount,
                range,
                samples = summarySample
            };
            var checkpointPayload = JsonSerializer.Serialize(checkpointObject);
            var planSummary = $"dry-run 计划完成：类型={archiveTask.TaskType}，保留天数={archiveTask.RetentionDays}，候选数量={plannedItemCount}，截止时间={cutoffTime:yyyy-MM-dd HH:mm:ss}。";
            return (plannedItemCount, planSummary, checkpointPayload);
        }
        catch (Exception ex) {
            Logger.Error(ex, "构建归档 dry-run 计划失败，TaskId={TaskId}, TaskType={TaskType}, RetentionDays={RetentionDays}", archiveTask.Id, archiveTask.TaskType, archiveTask.RetentionDays);
            throw;
        }
    }
}
