using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>
/// Outbox 消息仓储实现。
/// </summary>
public sealed class OutboxMessageRepository : RepositoryBase<OutboxMessage, SortingHubDbContext>, IOutboxMessageRepository {
    /// <summary>
    /// 原子领取可派发消息的最大重试次数。
    /// 当前固定为 8 次，兼顾常规并发冲突退避与避免后台线程长时间自旋。
    /// </summary>
    private const int MaxAcquireAttempts = 8;

    /// <summary>
    /// 初始化 Outbox 消息仓储。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public OutboxMessageRepository(IDbContextFactory<SortingHubDbContext> contextFactory)
        : base(contextFactory, LogManager.GetCurrentClassLogger()) {
    }

    /// <summary>
    /// 新增 Outbox 消息。
    /// </summary>
    /// <param name="outboxMessage">Outbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public override async Task<RepositoryResult> AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken) {
        if (outboxMessage is null) {
            return RepositoryResult.Fail("Outbox 消息不能为空。");
        }

        var safeEventType = SanitizeForLog(outboxMessage.EventType);
        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception exception) {
            Logger.Error(exception, "新增 Outbox 消息失败，EventType={EventType}", safeEventType);
            return RepositoryResult.Fail("新增 Outbox 消息失败。");
        }
    }

    /// <summary>
    /// 根据主键获取 Outbox 消息。
    /// </summary>
    /// <param name="id">消息主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Outbox 消息；不存在时返回 null。</returns>
    public async Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        if (id <= 0) {
            return null;
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<OutboxMessage>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception exception) {
            Logger.Error(exception, "查询 Outbox 消息失败，MessageId={MessageId}", id);
            throw;
        }
    }

    /// <summary>
    /// 分页查询 Outbox 消息。
    /// </summary>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="status">状态过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    public async Task<PageResult<OutboxMessage>> GetPagedAsync(
        PageRequest pageRequest,
        OutboxMessageStatus? status,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(pageRequest);

        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();
        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = dbContext.Set<OutboxMessage>().AsNoTracking();
            if (status.HasValue) {
                query = query.Where(x => x.Status == status.Value);
            }

            var totalCount = await query.LongCountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            return new PageResult<OutboxMessage> {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "分页查询 Outbox 消息失败，PageNumber={PageNumber}, PageSize={PageSize}, Status={Status}", pageNumber, pageSize, status);
            throw;
        }
    }

    /// <summary>
    /// 原子领取下一条可派发消息，并切换到处理中。
    /// </summary>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功领取的消息；不存在时返回 null。</returns>
    public async Task<OutboxMessage?> TryAcquireNextDispatchableAsync(int maxRetryCount, CancellationToken cancellationToken) {
        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "最大重试次数必须大于 0。");
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            for (var attempt = 0; attempt < MaxAcquireAttempts; attempt++) {
                var nextMessage = await dbContext.Set<OutboxMessage>()
                    .Where(x => x.Status == OutboxMessageStatus.Pending
                                || (x.Status == OutboxMessageStatus.Failed && x.RetryCount < maxRetryCount))
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (nextMessage is null) {
                    return null;
                }

                try {
                    nextMessage.MarkProcessing();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return nextMessage;
                }
                catch (DbUpdateConcurrencyException exception) {
                    Logger.Warn(exception, "领取 Outbox 消息发生并发冲突，MessageId={MessageId}, Attempt={Attempt}", nextMessage.Id, attempt + 1);
                    dbContext.ChangeTracker.Clear();
                }
            }

            Logger.Warn("Outbox 消息领取重试次数已耗尽，未成功获取可派发消息。");
            return null;
        }
        catch (Exception exception) {
            Logger.Error(exception, "获取可派发 Outbox 消息失败。");
            throw;
        }
    }

    /// <summary>
    /// 获取 Outbox 健康快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康快照读模型。</returns>
    public async Task<OutboxMessageHealthSnapshotReadModel> GetHealthSnapshotAsync(CancellationToken cancellationToken) {
        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var query = dbContext.Set<OutboxMessage>().AsNoTracking();
            var pendingCount = await query.LongCountAsync(x => x.Status == OutboxMessageStatus.Pending, cancellationToken);
            var processingCount = await query.LongCountAsync(x => x.Status == OutboxMessageStatus.Processing, cancellationToken);
            var failedCount = await query.LongCountAsync(x => x.Status == OutboxMessageStatus.Failed, cancellationToken);
            var deadLetteredCount = await query.LongCountAsync(x => x.Status == OutboxMessageStatus.DeadLettered, cancellationToken);
            var oldestActiveCreatedAt = await query
                .Where(x => x.Status == OutboxMessageStatus.Pending
                            || x.Status == OutboxMessageStatus.Processing
                            || x.Status == OutboxMessageStatus.Failed)
                .OrderBy(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return new OutboxMessageHealthSnapshotReadModel {
                PendingCount = pendingCount,
                ProcessingCount = processingCount,
                FailedCount = failedCount,
                DeadLetteredCount = deadLetteredCount,
                OldestActiveCreatedAt = oldestActiveCreatedAt
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "读取 Outbox 健康快照失败。");
            throw;
        }
    }

    /// <summary>
    /// 更新 Outbox 消息。
    /// </summary>
    /// <param name="outboxMessage">Outbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public override async Task<RepositoryResult> UpdateAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken) {
        if (outboxMessage is null) {
            return RepositoryResult.Fail("Outbox 消息不能为空。");
        }

        try {
            await using var dbContext = await ContextFactory.CreateDbContextAsync(cancellationToken);
            var persistedMessage = await dbContext.Set<OutboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == outboxMessage.Id, cancellationToken);
            if (persistedMessage is null) {
                return RepositoryResult.Fail("Outbox 消息不存在。");
            }

            dbContext.Entry(persistedMessage).CurrentValues.SetValues(outboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (DbUpdateConcurrencyException exception) {
            Logger.Warn(exception, "更新 Outbox 消息发生并发冲突，MessageId={MessageId}, Status={Status}", outboxMessage.Id, outboxMessage.Status);
            return RepositoryResult.Fail("Outbox 消息状态已发生变化，请刷新后重试。");
        }
        catch (Exception exception) {
            Logger.Error(exception, "更新 Outbox 消息失败，MessageId={MessageId}, Status={Status}", outboxMessage.Id, outboxMessage.Status);
            return RepositoryResult.Fail("更新 Outbox 消息失败。");
        }
    }

    /// <summary>
    /// 清理日志字段中的换行符，避免日志注入。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <returns>单行日志值。</returns>
    private static string SanitizeForLog(string value) {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
