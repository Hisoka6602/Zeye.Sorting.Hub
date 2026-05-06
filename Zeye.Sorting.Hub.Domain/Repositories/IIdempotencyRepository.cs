using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>
/// 幂等记录仓储契约。
/// </summary>
public interface IIdempotencyRepository {
    /// <summary>
    /// 新增幂等记录。
    /// </summary>
    /// <param name="idempotencyRecord">幂等记录聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> AddAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken);

    /// <summary>
    /// 按幂等键读取幂等记录。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="payloadHash">载荷哈希。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>幂等记录；不存在时返回 null。</returns>
    Task<IdempotencyRecord?> GetByKeyAsync(
        string sourceSystem,
        string operationName,
        string businessKey,
        string payloadHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// 更新幂等记录。
    /// </summary>
    /// <param name="idempotencyRecord">幂等记录聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> UpdateAsync(IdempotencyRecord idempotencyRecord, CancellationToken cancellationToken);
}
