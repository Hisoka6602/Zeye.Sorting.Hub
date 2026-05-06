using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>
/// 幂等记录仓储实现。
/// </summary>
public sealed class IdempotencyRepository : IIdempotencyRepository {
    /// <summary>
    /// 幂等记录冲突错误消息。
    /// </summary>
    private const string DuplicateRecordErrorMessage = "幂等记录已存在。";

    /// <summary>
    /// 数据库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _contextFactory;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 初始化幂等记录仓储。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public IdempotencyRepository(IDbContextFactory<SortingHubDbContext> contextFactory) {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// 新增幂等记录。
    /// </summary>
    /// <param name="idempotencyRecord">幂等记录聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public async Task<RepositoryResult> AddAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken) {
        if (idempotencyRecord is null) {
            return RepositoryResult.Fail("幂等记录不能为空。");
        }

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<IdempotencyRecord>().AddAsync(idempotencyRecord, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (OperationCanceledException ex) {
            Logger.Warn(ex, "新增幂等记录操作被取消，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", idempotencyRecord.SourceSystem, idempotencyRecord.OperationName, idempotencyRecord.BusinessKey);
            return RepositoryResult.Fail("操作已取消。");
        }
        catch (DbUpdateException ex) when (DuplicateKeyExceptionDetector.IsDuplicateKeyException(ex)) {
            Logger.Warn(ex, "新增幂等记录发生唯一键冲突，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", idempotencyRecord.SourceSystem, idempotencyRecord.OperationName, idempotencyRecord.BusinessKey);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.IdempotencyRecordConflict);
        }
        catch (DbUpdateException ex) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.Message) || DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.InnerException?.Message)) {
            Logger.Warn(ex, "新增幂等记录发生唯一键冲突（消息兜底分支），SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", idempotencyRecord.SourceSystem, idempotencyRecord.OperationName, idempotencyRecord.BusinessKey);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.IdempotencyRecordConflict);
        }
        catch (InvalidOperationException ex) when (DuplicateKeyExceptionDetector.ContainsDuplicateKeyMessage(ex.Message)) {
            Logger.Warn(ex, "新增幂等记录发生唯一键冲突（提供器兜底分支），SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", idempotencyRecord.SourceSystem, idempotencyRecord.OperationName, idempotencyRecord.BusinessKey);
            return RepositoryResult.Fail(DuplicateRecordErrorMessage, RepositoryErrorCodes.IdempotencyRecordConflict);
        }
        catch (Exception ex) {
            Logger.Error(ex, "新增幂等记录失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", idempotencyRecord.SourceSystem, idempotencyRecord.OperationName, idempotencyRecord.BusinessKey);
            return RepositoryResult.Fail("新增幂等记录失败。");
        }
    }

    /// <summary>
    /// 按幂等键读取幂等记录。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="payloadHash">载荷哈希。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>幂等记录；不存在时返回 null。</returns>
    public async Task<IdempotencyRecord?> GetByKeyAsync(
        string sourceSystem,
        string operationName,
        string businessKey,
        string payloadHash,
        CancellationToken cancellationToken) {
        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<IdempotencyRecord>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.SourceSystem == sourceSystem
                         && x.OperationName == operationName
                         && x.BusinessKey == businessKey
                         && x.PayloadHash == payloadHash,
                    cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "读取幂等记录失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}", sourceSystem, operationName, businessKey);
            throw;
        }
    }

    /// <summary>
    /// 更新幂等记录。
    /// </summary>
    /// <param name="idempotencyRecord">幂等记录聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public async Task<RepositoryResult> UpdateAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken) {
        if (idempotencyRecord is null) {
            return RepositoryResult.Fail("幂等记录不能为空。");
        }

        try {
            await using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var persistedRecord = await dbContext.Set<IdempotencyRecord>()
                .FirstOrDefaultAsync(x => x.Id == idempotencyRecord.Id, cancellationToken);
            if (persistedRecord is null) {
                return RepositoryResult.Fail("幂等记录不存在。");
            }

            dbContext.Entry(persistedRecord).CurrentValues.SetValues(idempotencyRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RepositoryResult.Success();
        }
        catch (Exception ex) {
            Logger.Error(ex, "更新幂等记录失败，RecordId={RecordId}, Status={Status}", idempotencyRecord.Id, idempotencyRecord.Status);
            return RepositoryResult.Fail("更新幂等记录失败。");
        }
    }
}
