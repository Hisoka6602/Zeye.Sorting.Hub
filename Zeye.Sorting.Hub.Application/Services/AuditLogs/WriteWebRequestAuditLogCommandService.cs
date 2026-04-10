using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.AuditLogs;

/// <summary>
/// Web 请求审计日志写入命令服务。
/// </summary>
public sealed class WriteWebRequestAuditLogCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

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
    /// <param name="auditLog">审计日志聚合，不能为 null。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入结果；失败时包含错误信息。</returns>
    public async Task<RepositoryResult> WriteAsync(
        WebRequestAuditLog auditLog,
        CancellationToken cancellationToken) {
        if (auditLog is null) {
            throw new ArgumentNullException(nameof(auditLog));
        }

        try {
            return await _webRequestAuditLogRepository.AddAsync(auditLog, cancellationToken);
        }
        catch (Exception ex) {
            Logger.Error(ex, "写入 Web 请求审计日志失败，TraceId={TraceId}", auditLog.TraceId);
            throw;
        }
    }
}
