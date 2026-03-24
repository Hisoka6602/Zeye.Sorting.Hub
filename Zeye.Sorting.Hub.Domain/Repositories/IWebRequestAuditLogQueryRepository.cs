using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>
/// Web 请求审计日志只读查询仓储契约。
/// </summary>
public interface IWebRequestAuditLogQueryRepository {
    /// <summary>
    /// 按过滤条件分页查询审计日志列表摘要。
    /// </summary>
    /// <param name="filter">查询过滤参数。</param>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页摘要结果。</returns>
    Task<PageResult<WebRequestAuditLogSummaryReadModel>> GetPagedAsync(
        WebRequestAuditLogQueryFilter filter,
        PageRequest pageRequest,
        CancellationToken cancellationToken);

    /// <summary>
    /// 按主键查询审计日志详情。
    /// </summary>
    /// <param name="id">审计日志主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>审计日志详情读模型；不存在时返回 null。</returns>
    Task<WebRequestAuditLogDetailReadModel?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
