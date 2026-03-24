using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.AuditLogs {

    /// <summary>
    /// Web 请求审计日志写入命令服务。
    /// </summary>
    public sealed class WriteWebRequestAuditLogCommandService {
        /// <summary>
        /// Web 请求审计日志仓储。
        /// </summary>
        private readonly IWebRequestAuditLogRepository _webRequestAuditLogRepository;

        /// <summary>
        /// 创建写入命令服务。
        /// </summary>
        /// <param name="webRequestAuditLogRepository">审计日志仓储。</param>
        public WriteWebRequestAuditLogCommandService(IWebRequestAuditLogRepository webRequestAuditLogRepository) {
            _webRequestAuditLogRepository = webRequestAuditLogRepository ?? throw new ArgumentNullException(nameof(webRequestAuditLogRepository));
        }

        /// <summary>
        /// 写入 Web 请求审计日志聚合（包含冷热一对一详情）。
        /// </summary>
        /// <param name="auditLog">审计日志聚合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>写入结果；失败时包含错误信息。</returns>
        public Task<Domain.Repositories.Models.Results.RepositoryResult> WriteAsync(
            WebRequestAuditLog auditLog,
            CancellationToken cancellationToken) {
            return _webRequestAuditLogRepository.AddAsync(auditLog, cancellationToken);
        }
    }
}
