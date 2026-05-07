using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>
/// Outbox 消息仓储契约。
/// </summary>
public interface IOutboxMessageRepository {
    /// <summary>
    /// 新增 Outbox 消息。
    /// </summary>
    /// <param name="outboxMessage">Outbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken);

    /// <summary>
    /// 根据主键获取 Outbox 消息。
    /// </summary>
    /// <param name="id">消息主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Outbox 消息；不存在时返回 null。</returns>
    Task<OutboxMessage?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询 Outbox 消息。
    /// </summary>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="status">状态过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    Task<PageResult<OutboxMessage>> GetPagedAsync(
        PageRequest pageRequest,
        OutboxMessageStatus? status,
        CancellationToken cancellationToken);

    /// <summary>
    /// 原子领取下一条可派发消息，并切换到处理中。
    /// </summary>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功领取的消息；不存在时返回 null。</returns>
    Task<OutboxMessage?> TryAcquireNextDispatchableAsync(int maxRetryCount, CancellationToken cancellationToken);

    /// <summary>
    /// 获取 Outbox 健康快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康快照读模型。</returns>
    Task<OutboxMessageHealthSnapshotReadModel> GetHealthSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 更新 Outbox 消息。
    /// </summary>
    /// <param name="outboxMessage">Outbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> UpdateAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken);
}
