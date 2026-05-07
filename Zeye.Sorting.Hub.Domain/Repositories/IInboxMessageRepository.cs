using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>
/// Inbox 消息仓储契约。
/// </summary>
public interface IInboxMessageRepository {
    /// <summary>
    /// 新增 Inbox 消息。
    /// </summary>
    /// <param name="inboxMessage">Inbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> AddAsync(InboxMessage inboxMessage, CancellationToken cancellationToken);

    /// <summary>
    /// 按来源系统与消息标识读取 Inbox 消息。
    /// </summary>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Inbox 消息；不存在时返回 null。</returns>
    Task<InboxMessage?> GetByKeyAsync(string sourceSystem, string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取已到过期治理时间的清理候选。
    /// </summary>
    /// <param name="expireBefore">过期治理截止时间。</param>
    /// <param name="take">最大返回数量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>过期治理候选列表。</returns>
    Task<IReadOnlyList<InboxMessage>> GetCleanupCandidatesAsync(
        DateTime expireBefore,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// 更新 Inbox 消息。
    /// </summary>
    /// <param name="inboxMessage">Inbox 消息聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> UpdateAsync(InboxMessage inboxMessage, CancellationToken cancellationToken);
}
