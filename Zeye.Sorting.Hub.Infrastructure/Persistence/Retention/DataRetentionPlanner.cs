using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
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
/// 数据保留计划器。
/// </summary>
public sealed class DataRetentionPlanner {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
    /// 数据保留配置。
    /// </summary>
    private readonly DataRetentionOptions _options;

    /// <summary>
    /// 初始化数据保留计划器。
    /// </summary>
    /// <param name="dbContextFactory">数据库上下文工厂。</param>
    /// <param name="deadLetterWriteStore">死信存储。</param>
    /// <param name="slowQueryProfileStore">慢查询画像存储。</param>
    /// <param name="options">数据保留配置。</param>
    public DataRetentionPlanner(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        DeadLetterWriteStore deadLetterWriteStore,
        SlowQueryProfileStore slowQueryProfileStore,
        IOptions<DataRetentionOptions> options) {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _deadLetterWriteStore = deadLetterWriteStore ?? throw new ArgumentNullException(nameof(deadLetterWriteStore));
        _slowQueryProfileStore = slowQueryProfileStore ?? throw new ArgumentNullException(nameof(slowQueryProfileStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 获取当前有效策略。
    /// </summary>
    /// <returns>策略清单。</returns>
    public IReadOnlyList<DataRetentionPolicy> GetEffectivePolicies() {
        return _options.Policies
            .Where(static policy => DataRetentionPolicy.IsSupportedName(policy.Name) && DataRetentionPolicy.IsValidRetentionDays(policy.RetentionDays))
            .Select(static policy => new DataRetentionPolicy {
                Name = policy.Name.Trim(),
                RetentionDays = policy.RetentionDays
            })
            .ToArray();
    }

    /// <summary>
    /// 计算策略的过期截止时间。
    /// </summary>
    /// <param name="policy">策略。</param>
    /// <param name="now">当前时间。</param>
    /// <returns>过期截止时间。</returns>
    public static DateTime BuildExpireBefore(DataRetentionPolicy policy, DateTime now) {
        ArgumentNullException.ThrowIfNull(policy);
        return now.AddDays(-policy.RetentionDays);
    }

    /// <summary>
    /// 统计单个策略的计划处理量。
    /// </summary>
    /// <param name="policy">策略。</param>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    public async Task<int> CountPlannedAsync(
        DataRetentionPolicy policy,
        DateTime expireBefore,
        int batchSize,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(policy);
        if (batchSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "批次大小必须大于 0。");
        }

        try {
            return policy.Name switch {
                DataRetentionPolicy.WebRequestAuditLogName => await CountAsync<WebRequestAuditLog>(
                    query => query.Where(x => x.CreatedAt <= expireBefore),
                    query => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.OutboxMessageName => await CountAsync<OutboxMessage>(
                    query => query.Where(x => (x.Status == OutboxMessageStatus.Succeeded || x.Status == OutboxMessageStatus.DeadLettered)
                                             && (x.CompletedAt ?? x.UpdatedAt) <= expireBefore),
                    query => query.OrderBy(x => x.CompletedAt ?? x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.InboxMessageName => await CountAsync<InboxMessage>(
                    query => query.Where(x => x.Status != InboxMessageStatus.Processing && x.ExpiresAt <= expireBefore),
                    query => query.OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.IdempotencyRecordName => await CountAsync<IdempotencyRecord>(
                    query => query.Where(x => x.Status != IdempotencyRecordStatus.Pending && x.UpdatedAt <= expireBefore),
                    query => query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.ArchiveTaskName => await CountAsync<ArchiveTask>(
                    query => query.Where(x => x.Status != ArchiveTaskStatus.Pending && x.Status != ArchiveTaskStatus.Running && (x.CompletedAt ?? x.UpdatedAt) <= expireBefore),
                    query => query.OrderBy(x => x.CompletedAt ?? x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.DeadLetterWriteEntryName => _deadLetterWriteStore.CountExpired(expireBefore, batchSize),
                DataRetentionPolicy.SlowQueryProfileName => _slowQueryProfileStore.CountRetentionCandidates(expireBefore, batchSize),
                _ => 0
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "统计数据保留候选失败，PolicyName={PolicyName}, ExpireBefore={ExpireBefore}, BatchSize={BatchSize}", policy.Name, expireBefore, batchSize);
            throw;
        }
    }

    /// <summary>
    /// 执行单个策略的真实清理。
    /// </summary>
    /// <param name="policy">策略。</param>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已处理数量。</returns>
    public async Task<int> ExecuteAsync(
        DataRetentionPolicy policy,
        DateTime expireBefore,
        int batchSize,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(policy);
        if (batchSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "批次大小必须大于 0。");
        }

        try {
            return policy.Name switch {
                DataRetentionPolicy.WebRequestAuditLogName => await DeleteWebRequestAuditLogsAsync(expireBefore, batchSize, cancellationToken),
                DataRetentionPolicy.OutboxMessageName => await DeleteAsync<OutboxMessage>(
                    query => query.Where(x => (x.Status == OutboxMessageStatus.Succeeded || x.Status == OutboxMessageStatus.DeadLettered)
                                             && (x.CompletedAt ?? x.UpdatedAt) <= expireBefore),
                    query => query.OrderBy(x => x.CompletedAt ?? x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.InboxMessageName => await DeleteAsync<InboxMessage>(
                    query => query.Where(x => x.Status != InboxMessageStatus.Processing && x.ExpiresAt <= expireBefore),
                    query => query.OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.IdempotencyRecordName => await DeleteAsync<IdempotencyRecord>(
                    query => query.Where(x => x.Status != IdempotencyRecordStatus.Pending && x.UpdatedAt <= expireBefore),
                    query => query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.ArchiveTaskName => await DeleteAsync<ArchiveTask>(
                    query => query.Where(x => x.Status != ArchiveTaskStatus.Pending && x.Status != ArchiveTaskStatus.Running && (x.CompletedAt ?? x.UpdatedAt) <= expireBefore),
                    query => query.OrderBy(x => x.CompletedAt ?? x.UpdatedAt).ThenBy(x => x.Id),
                    batchSize,
                    cancellationToken),
                DataRetentionPolicy.DeadLetterWriteEntryName => _deadLetterWriteStore.RemoveExpired(expireBefore, batchSize),
                DataRetentionPolicy.SlowQueryProfileName => _slowQueryProfileStore.RemoveRetentionCandidates(expireBefore, batchSize),
                _ => 0
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "执行数据保留清理失败，PolicyName={PolicyName}, ExpireBefore={ExpireBefore}, BatchSize={BatchSize}", policy.Name, expireBefore, batchSize);
            throw;
        }
    }

    /// <summary>
    /// 统计受限批次内的候选数量。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="whereBuilder">过滤构建器。</param>
    /// <param name="orderBuilder">排序构建器。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>候选数量。</returns>
    private async Task<int> CountAsync<TEntity>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> whereBuilder,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBuilder,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : class {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = whereBuilder(dbContext.Set<TEntity>().AsNoTracking());
        var markers = await orderBuilder(query)
            .Select(static _ => 1)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        return markers.Count;
    }

    /// <summary>
    /// 删除受限批次内的候选实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="whereBuilder">过滤构建器。</param>
    /// <param name="orderBuilder">排序构建器。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除数量。</returns>
    private async Task<int> DeleteAsync<TEntity>(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> whereBuilder,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBuilder,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : class {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await orderBuilder(whereBuilder(dbContext.Set<TEntity>()))
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        if (entities.Count == 0) {
            return 0;
        }

        dbContext.Set<TEntity>().RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }

    /// <summary>
    /// 删除受限批次内的 Web 请求审计日志。
    /// </summary>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除数量。</returns>
    private async Task<int> DeleteWebRequestAuditLogsAsync(DateTime expireBefore, int batchSize, CancellationToken cancellationToken) {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<WebRequestAuditLog>()
            .Include(x => x.Detail)
            .Where(x => x.CreatedAt <= expireBefore)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        if (entities.Count == 0) {
            return 0;
        }

        dbContext.Set<WebRequestAuditLog>().RemoveRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }
}
