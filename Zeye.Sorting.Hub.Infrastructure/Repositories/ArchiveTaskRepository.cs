using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>
/// 归档任务仓储实现。
/// </summary>
public sealed class ArchiveTaskRepository : RepositoryBase<ArchiveTask, SortingHubDbContext>, IArchiveTaskRepository {
    /// <summary>
    /// 原子领取待执行任务的最大重试次数。
    /// </summary>
    private const int MaxAcquireAttempts = 8;

    /// <summary>
    /// 初始化归档任务仓储。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public ArchiveTaskRepository(IDbContextFactory<SortingHubDbContext> contextFactory)
        : base(contextFactory, LogManager.GetCurrentClassLogger()) {
    }

    /// <summary>
    /// 新增归档任务。
    /// </summary>
    /// <param name="archiveTask">归档任务聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public override async Task<RepositoryResult> AddAsync(ArchiveTask archiveTask, CancellationToken cancellationToken) {
        if (archiveTask is null) {
            return RepositoryResult.Fail("归档任务不能为空。");
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<ArchiveTask>().AddAsync(archiveTask, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception ex) {
            Logger.Error(ex, "新增归档任务失败，TaskType={TaskType}, RequestedBy={RequestedBy}", archiveTask.TaskType, archiveTask.RequestedBy);
            return RepositoryResult.Fail("新增归档任务失败。");
        }
    }

    /// <summary>
    /// 根据主键获取归档任务。
    /// </summary>
    /// <param name="id">任务主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档任务；不存在时返回 null。</returns>
    public async Task<ArchiveTask?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        if (id <= 0) {
            return null;
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<ArchiveTask>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "查询归档任务失败，TaskId={TaskId}", id);
            throw;
        }
    }

    /// <summary>
    /// 分页查询归档任务。
    /// </summary>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="status">状态过滤。</param>
    /// <param name="taskType">类型过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    public async Task<PageResult<ArchiveTask>> GetPagedAsync(
        PageRequest pageRequest,
        ArchiveTaskStatus? status,
        ArchiveTaskType? taskType,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(pageRequest);

        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();
        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = dbContext.Set<ArchiveTask>().AsNoTracking();
            if (status.HasValue) {
                query = query.Where(x => x.Status == status.Value);
            }

            if (taskType.HasValue) {
                query = query.Where(x => x.TaskType == taskType.Value);
            }

            var totalCount = await query.LongCountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return new PageResult<ArchiveTask> {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex) {
            Logger.Error(ex, "分页查询归档任务失败，PageNumber={PageNumber}, PageSize={PageSize}, Status={Status}, TaskType={TaskType}", pageNumber, pageSize, status, taskType);
            throw;
        }
    }

    /// <summary>
    /// 原子领取最早创建的待执行任务，并将其标记为执行中。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功领取的任务；不存在时返回 null。</returns>
    public async Task<ArchiveTask?> TryAcquireNextPendingAsync(CancellationToken cancellationToken) {
        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            for (var attempt = 0; attempt < MaxAcquireAttempts; attempt++) {
                // 步骤 1：按最早创建顺序读取一个 Pending 任务，保持跟踪状态以便并发令牌参与更新。
                var nextPendingTask = await dbContext.Set<ArchiveTask>()
                    .Where(x => x.Status == ArchiveTaskStatus.Pending)
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (nextPendingTask is null) {
                    return null;
                }

                try {
                    // 步骤 2：直接在同一上下文内切换到 Running。
                    //         Status 被配置为并发令牌，最终 UPDATE 会自动附带原始 Status=Pending 条件，
                    //         从而保证只有一个执行器能成功领取该任务。
                    nextPendingTask.MarkRunning();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return nextPendingTask;
                }
                catch (DbUpdateConcurrencyException ex) {
                    Logger.Warn(ex, "归档任务领取发生并发冲突，TaskId={TaskId}, Attempt={Attempt}", nextPendingTask.Id, attempt + 1);
                    dbContext.ChangeTracker.Clear();
                }
            }

            Logger.Warn("归档任务领取重试次数已耗尽，未成功获取可执行任务。");
            return null;
        }
        catch (Exception ex) {
            Logger.Error(ex, "获取待执行归档任务失败。");
            throw;
        }
    }

    /// <summary>
    /// 更新归档任务。
    /// </summary>
    /// <param name="archiveTask">归档任务聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public override async Task<RepositoryResult> UpdateAsync(ArchiveTask archiveTask, CancellationToken cancellationToken) {
        if (archiveTask is null) {
            return RepositoryResult.Fail("归档任务不能为空。");
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var persistedTask = await dbContext.Set<ArchiveTask>()
                .FirstOrDefaultAsync(x => x.Id == archiveTask.Id, cancellationToken);
            if (persistedTask is null) {
                return RepositoryResult.Fail("归档任务不存在。");
            }

            dbContext.Entry(persistedTask).CurrentValues.SetValues(archiveTask);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (DbUpdateConcurrencyException ex) {
            Logger.Warn(ex, "更新归档任务发生并发冲突，TaskId={TaskId}, Status={Status}", archiveTask.Id, archiveTask.Status);
            return RepositoryResult.Fail("归档任务状态已发生变化，请刷新后重试。");
        }
        catch (Exception ex) {
            Logger.Error(ex, "更新归档任务失败，TaskId={TaskId}, Status={Status}", archiveTask.Id, archiveTask.Status);
            return RepositoryResult.Fail("更新归档任务失败。");
        }
    }
}
