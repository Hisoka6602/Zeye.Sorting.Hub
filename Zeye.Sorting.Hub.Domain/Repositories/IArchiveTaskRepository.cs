using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>
/// 归档任务仓储契约。
/// </summary>
public interface IArchiveTaskRepository {
    /// <summary>
    /// 新增归档任务。
    /// </summary>
    /// <param name="archiveTask">归档任务聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> AddAsync(ArchiveTask archiveTask, CancellationToken cancellationToken);

    /// <summary>
    /// 根据主键获取归档任务。
    /// </summary>
    /// <param name="id">任务主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档任务；不存在时返回 null。</returns>
    Task<ArchiveTask?> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询归档任务。
    /// </summary>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="status">状态过滤。</param>
    /// <param name="taskType">类型过滤。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    Task<PageResult<ArchiveTask>> GetPagedAsync(
        PageRequest pageRequest,
        ArchiveTaskStatus? status,
        ArchiveTaskType? taskType,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取最早创建的待执行任务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>待执行任务；不存在时返回 null。</returns>
    Task<ArchiveTask?> GetNextPendingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 更新归档任务。
    /// </summary>
    /// <param name="archiveTask">归档任务聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    Task<RepositoryResult> UpdateAsync(ArchiveTask archiveTask, CancellationToken cancellationToken);
}
