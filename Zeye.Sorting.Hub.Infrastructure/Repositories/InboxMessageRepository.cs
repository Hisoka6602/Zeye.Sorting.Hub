using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>
/// Inbox 消息仓储实现。
/// </summary>
public sealed class InboxMessageRepository : IInboxMessageRepository {
    /// <summary>
    /// Inbox 消息冲突错误消息。
    /// </summary>
    private const string DuplicateRecordErrorMessage = "Inbox 消息已存在。";

    /// <summary>
    /// 数据库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _contextFactory;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 初始化 Inbox 消息仓储。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public InboxMessageRepository(IDbContextFactory<SortingHubDbContext> contextFactory) {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// 新增 Inbox 消息。
    /// </summary>
    /// <param name="inboxMessage">Inbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public async Task<RepositoryResult> AddAsync(InboxMessage inboxMessage, CancellationToken cancellationToken) {
        if (inboxMessage is null) {
            return RepositoryResult.Fail("Inbox 消息不能为空。");
        }

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<InboxMessage>().AddAsync(inboxMessage, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (DbUpdateException exception) when (DuplicateKeyExceptionDetector.IsDuplicateKeyException(exception)) {
            Logger.Warn(exception, "新增 Inbox 消息发生唯一键冲突，SourceSystem={SourceSystem}, MessageId={MessageId}", inboxMessage.SourceSystem, inboxMessage.MessageId);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.InboxMessageConflict);
        }
        catch (DbUpdateException exception) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(exception.Message) || DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(exception.InnerException?.Message)) {
            Logger.Warn(exception, "新增 Inbox 消息发生唯一键冲突（消息兜底分支），SourceSystem={SourceSystem}, MessageId={MessageId}", inboxMessage.SourceSystem, inboxMessage.MessageId);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.InboxMessageConflict);
        }
        catch (InvalidOperationException exception) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(exception.Message)) {
            Logger.Warn(exception, "新增 Inbox 消息发生唯一键冲突（提供器兜底分支），SourceSystem={SourceSystem}, MessageId={MessageId}", inboxMessage.SourceSystem, inboxMessage.MessageId);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.InboxMessageConflict);
        }
        catch (Exception exception) {
            Logger.Error(exception, "新增 Inbox 消息失败，SourceSystem={SourceSystem}, MessageId={MessageId}", inboxMessage.SourceSystem, inboxMessage.MessageId);
            return RepositoryResult.Fail("新增 Inbox 消息失败。");
        }
    }

    /// <summary>
    /// 按来源系统与消息标识读取 Inbox 消息。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Inbox 消息；不存在时返回 null。</returns>
    public async Task<InboxMessage?> GetByKeyAsync(string sourceSystem, string messageId, CancellationToken cancellationToken) {
        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.SourceSystem == sourceSystem && x.MessageId == messageId,
                    cancellationToken);
        }
        catch (Exception exception) {
            Logger.Error(exception, "读取 Inbox 消息失败，SourceSystem={SourceSystem}, MessageId={MessageId}", sourceSystem, messageId);
            throw;
        }
    }

    /// <summary>
    /// 获取已到过期治理时间的清理候选。
    /// </summary>
    /// <param name="expireBefore">过期治理截止时间。</param>
    /// <param name="take">最大返回数量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>过期治理候选列表。</returns>
    public async Task<IReadOnlyList<InboxMessage>> GetCleanupCandidatesAsync(
        DateTime expireBefore,
        int take,
        CancellationToken cancellationToken) {
        if (take <= 0) {
            throw new ArgumentOutOfRangeException(nameof(take), "take 必须大于 0。");
        }

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .Where(x => x.ExpiresAt <= expireBefore && x.Status != InboxMessageStatus.Processing)
                .OrderBy(x => x.ExpiresAt)
                .ThenBy(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) {
            Logger.Error(exception, "读取 Inbox 过期治理候选失败，ExpireBefore={ExpireBefore}, Take={Take}", expireBefore, take);
            throw;
        }
    }

    /// <summary>
    /// 更新 Inbox 消息。
    /// </summary>
    /// <param name="inboxMessage">Inbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public async Task<RepositoryResult> UpdateAsync(InboxMessage inboxMessage, CancellationToken cancellationToken) {
        if (inboxMessage is null) {
            return RepositoryResult.Fail("Inbox 消息不能为空。");
        }

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var persistedMessage = await dbContext.Set<InboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == inboxMessage.Id, cancellationToken);
            if (persistedMessage is null) {
                return RepositoryResult.Fail("Inbox 消息不存在。");
            }

            dbContext.Entry(persistedMessage).CurrentValues.SetValues(inboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception exception) {
            Logger.Error(exception, "更新 Inbox 消息失败，RecordId={RecordId}, Status={Status}", inboxMessage.Id, inboxMessage.Status);
            return RepositoryResult.Fail("更新 Inbox 消息失败。");
        }
    }
}
