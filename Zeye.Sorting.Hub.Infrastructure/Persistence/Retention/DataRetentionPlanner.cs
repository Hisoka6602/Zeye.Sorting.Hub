using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Enums.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理计划器。
/// </summary>
public sealed class DataRetentionPlanner {
    /// <summary>
    /// 数据库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _dbContextFactory;

    /// <summary>
    /// 死信存储。
    /// </summary>
    private readonly DeadLetterWriteStore _deadLetterWriteStore;

    /// <summary>
    /// 慢查询画像存储。
    /// </summary>
    private readonly SlowQueryProfileStore _slowQueryProfileStore;

    /// <summary>
    /// 初始化数据保留治理计划器。
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂。</param>
    /// <param name="deadLetterWriteStore">死信存储。</param>
    /// <param name="slowQueryProfileStore">慢查询画像存储。</param>
    public DataRetentionPlanner(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        DeadLetterWriteStore deadLetterWriteStore,
        SlowQueryProfileStore slowQueryProfileStore) {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _deadLetterWriteStore = deadLetterWriteStore ?? throw new ArgumentNullException(nameof(deadLetterWriteStore));
        _slowQueryProfileStore = slowQueryProfileStore ?? throw new ArgumentNullException(nameof(slowQueryProfileStore));
    }

    /// <summary>
    /// 构建本次数据保留治理候选计划。
    /// </summary>
    /// <param name="options">当前配置。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>各策略候选数量。</returns>
    public async Task<IReadOnlyDictionary<string, int>> BuildCandidateCountsAsync(DataRetentionOptions options, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(options);

        var candidateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var policy in options.Policies) {
            cancellationToken.ThrowIfCancellationRequested();
            var count = await CountCandidatesByPolicyAsync(dbContext, policy, options.BatchSize, cancellationToken);
            candidateCounts[policy.Name] = count;
        }

        return candidateCounts;
    }

    /// <summary>
    /// 按策略统计候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="policy">策略项。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private Task<int> CountCandidatesByPolicyAsync(
        SortingHubDbContext dbContext,
        DataRetentionPolicy policy,
        int batchSize,
        CancellationToken cancellationToken) {
        var cutoffTime = DateTime.Now.AddDays(-policy.RetentionDays);
        return policy.Name switch {
            DataRetentionPolicy.WebRequestAuditLog => CountWebRequestAuditLogCandidatesAsync(dbContext, cutoffTime, batchSize, cancellationToken),
            DataRetentionPolicy.OutboxMessage => CountOutboxMessageCandidatesAsync(dbContext, cutoffTime, batchSize, cancellationToken),
            DataRetentionPolicy.InboxMessage => CountInboxMessageCandidatesAsync(dbContext, cutoffTime, batchSize, cancellationToken),
            DataRetentionPolicy.IdempotencyRecord => CountIdempotencyCandidatesAsync(dbContext, cutoffTime, batchSize, cancellationToken),
            DataRetentionPolicy.ArchiveTask => CountArchiveTaskCandidatesAsync(dbContext, cutoffTime, batchSize, cancellationToken),
            DataRetentionPolicy.DeadLetterWriteEntry => Task.FromResult(CountDeadLetterCandidates(cutoffTime, batchSize)),
            DataRetentionPolicy.SlowQueryProfile => Task.FromResult(_slowQueryProfileStore.GetRetentionCandidateCount(cutoffTime, batchSize)),
            _ => Task.FromResult(0)
        };
    }

    /// <summary>
    /// 统计 Web 请求审计日志候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private static Task<int> CountWebRequestAuditLogCandidatesAsync(
        SortingHubDbContext dbContext,
        DateTime cutoffTime,
        int batchSize,
        CancellationToken cancellationToken) {
        return dbContext.Set<WebRequestAuditLog>()
            .AsNoTracking()
            .Where(static log => true)
            .Where(log => log.CreatedAt <= cutoffTime)
            .OrderBy(log => log.CreatedAt)
            .Select(log => log.Id)
            .Take(batchSize)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 统计 Outbox 消息候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private static Task<int> CountOutboxMessageCandidatesAsync(
        SortingHubDbContext dbContext,
        DateTime cutoffTime,
        int batchSize,
        CancellationToken cancellationToken) {
        return dbContext.Set<OutboxMessage>()
            .AsNoTracking()
            .Where(message => (message.Status == OutboxMessageStatus.Succeeded || message.Status == OutboxMessageStatus.DeadLettered)
                              && message.UpdatedAt <= cutoffTime)
            .OrderBy(message => message.UpdatedAt)
            .Select(message => message.Id)
            .Take(batchSize)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 统计 Inbox 消息候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private static Task<int> CountInboxMessageCandidatesAsync(
        SortingHubDbContext dbContext,
        DateTime cutoffTime,
        int batchSize,
        CancellationToken cancellationToken) {
        var now = DateTime.Now;
        return dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .Where(message => message.Status != InboxMessageStatus.Processing
                              && (message.ExpiresAt <= now || message.UpdatedAt <= cutoffTime))
            .OrderBy(message => message.ExpiresAt)
            .ThenBy(message => message.Id)
            .Select(message => message.Id)
            .Take(batchSize)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 统计幂等记录候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private static Task<int> CountIdempotencyCandidatesAsync(
        SortingHubDbContext dbContext,
        DateTime cutoffTime,
        int batchSize,
        CancellationToken cancellationToken) {
        return dbContext.Set<IdempotencyRecord>()
            .AsNoTracking()
            .Where(record => record.Status != IdempotencyRecordStatus.Pending && record.UpdatedAt <= cutoffTime)
            .OrderBy(record => record.UpdatedAt)
            .Select(record => record.Id)
            .Take(batchSize)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 统计归档任务候选数量。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private static Task<int> CountArchiveTaskCandidatesAsync(
        SortingHubDbContext dbContext,
        DateTime cutoffTime,
        int batchSize,
        CancellationToken cancellationToken) {
        return dbContext.Set<ArchiveTask>()
            .AsNoTracking()
            .Where(task => task.Status != ArchiveTaskStatus.Pending
                           && task.Status != ArchiveTaskStatus.Running
                           && task.CompletedAt.HasValue
                           && task.CompletedAt.Value <= cutoffTime)
            .OrderBy(task => task.CompletedAt)
            .Select(task => task.Id)
            .Take(batchSize)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// 统计死信记录候选数量。
    /// </summary>
    /// <param name="cutoffTime">截止时间。</param>
    /// <param name="batchSize">批次上限。</param>
    /// <returns>候选数量。</returns>
    private int CountDeadLetterCandidates(DateTime cutoffTime, int batchSize) {
        return _deadLetterWriteStore.GetSnapshot()
            .Where(entry => entry.FailedAtLocal <= cutoffTime)
            .Take(batchSize)
            .Count();
    }
}
