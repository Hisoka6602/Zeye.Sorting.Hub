using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories {

    /// <summary>
    /// Web 请求审计日志仓储契约。
    /// </summary>
    public interface IWebRequestAuditLogRepository {
        /// <summary>
        /// 新增 Web 请求审计日志聚合。
        /// </summary>
        /// <param name="auditLog">审计日志聚合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>仓储执行结果。</returns>
        Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken);
    }
}
