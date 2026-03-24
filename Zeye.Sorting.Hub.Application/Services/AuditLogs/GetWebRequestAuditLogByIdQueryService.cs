using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.AuditLogs;

/// <summary>
/// 按 Id 查询 Web 请求审计日志详情应用服务。
/// </summary>
public sealed class GetWebRequestAuditLogByIdQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 审计日志查询仓储。
    /// </summary>
    private readonly IWebRequestAuditLogQueryRepository _webRequestAuditLogQueryRepository;

    /// <summary>
    /// 初始化按 Id 查询服务。
    /// </summary>
    /// <param name="webRequestAuditLogQueryRepository">审计日志查询仓储。</param>
    public GetWebRequestAuditLogByIdQueryService(IWebRequestAuditLogQueryRepository webRequestAuditLogQueryRepository) {
        _webRequestAuditLogQueryRepository = webRequestAuditLogQueryRepository ?? throw new ArgumentNullException(nameof(webRequestAuditLogQueryRepository));
    }

    /// <summary>
    /// 执行按 Id 查询。
    /// </summary>
    /// <param name="id">主键 Id。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>详情响应，不存在时返回 null。</returns>
    public async Task<WebRequestAuditLogDetailResponse?> ExecuteAsync(long id, CancellationToken cancellationToken) {
        Guard.ThrowIfZeroOrNegative(id, nameof(id), "审计日志 Id 必须大于 0。", "按 Id 查询 Web 请求审计日志详情");

        try {
            var readModel = await _webRequestAuditLogQueryRepository.GetByIdAsync(id, cancellationToken);
            return readModel is null ? null : WebRequestAuditLogContractMapper.ToDetail(readModel);
        }
        catch (Exception exception) {
            Logger.Error(exception, "按 Id 查询 Web 请求审计日志详情失败，Id={AuditLogId}", id);
            throw;
        }
    }
}
