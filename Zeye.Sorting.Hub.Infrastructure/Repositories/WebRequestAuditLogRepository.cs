using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

    /// <summary>
    /// Web 请求审计日志仓储实现（热表+冷表同事务写入）。
    /// </summary>
    public sealed class WebRequestAuditLogRepository : RepositoryBase<WebRequestAuditLog, SortingHubDbContext>, IWebRequestAuditLogRepository {
        /// <summary>
        /// NLog 日志器（静态，无需 DI 注入；日志来源类名为 WebRequestAuditLogRepository）。
        /// </summary>
        private static readonly ILogger NLogLogger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 创建 WebRequestAuditLogRepository。
        /// </summary>
        /// <param name="contextFactory">DbContext 工厂。</param>
        public WebRequestAuditLogRepository(IDbContextFactory<SortingHubDbContext> contextFactory)
            : base(contextFactory, NLogLogger) {
        }

        /// <summary>
        /// 新增 Web 请求审计日志聚合，并在同一事务中落库冷表详情。
        /// </summary>
        /// <param name="auditLog">审计日志聚合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>仓储执行结果。</returns>
        public override async Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken) {
            if (auditLog is null) {
                return RepositoryResult.Fail("审计日志不能为空");
            }

            if (auditLog.Detail is not null && auditLog.Detail.StartedAt == default) {
                auditLog.Detail.StartedAt = auditLog.StartedAt;
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                await db.Set<WebRequestAuditLog>().AddAsync(auditLog, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException ex) {
                NLogLogger.Warn(ex, "写入 Web 请求审计日志被取消，TraceId={TraceId}, CorrelationId={CorrelationId}", auditLog.TraceId, auditLog.CorrelationId);
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                NLogLogger.Error(ex, "写入 Web 请求审计日志失败，TraceId={TraceId}, CorrelationId={CorrelationId}", auditLog.TraceId, auditLog.CorrelationId);
                return RepositoryResult.Fail("写入 Web 请求审计日志失败");
            }
        }
    }
}
