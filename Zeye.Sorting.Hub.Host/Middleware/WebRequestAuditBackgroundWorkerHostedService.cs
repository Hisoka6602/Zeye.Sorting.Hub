using NLog;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计后台消费服务。
/// </summary>
internal sealed class WebRequestAuditBackgroundWorkerHostedService : BackgroundService {
    /// <summary>
    /// NLog 记录器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();
    /// <summary>
    /// 后台队列实例。
    /// </summary>
    private readonly WebRequestAuditBackgroundQueue _queue;
    /// <summary>
    /// 服务作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 构造后台消费服务。
    /// </summary>
    /// <param name="queue">后台队列。</param>
    /// <param name="scopeFactory">服务作用域工厂。</param>
    public WebRequestAuditBackgroundWorkerHostedService(
        WebRequestAuditBackgroundQueue queue,
        IServiceScopeFactory scopeFactory) {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 后台消费主循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>异步任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await foreach (var entry in _queue.Reader.ReadAllAsync(stoppingToken)) {
            try {
                using var scope = _scopeFactory.CreateScope();
                var writeService = scope.ServiceProvider.GetRequiredService<WriteWebRequestAuditLogCommandService>();
                var result = await writeService.WriteAsync(entry.Log, stoppingToken);
                if (!result.IsSuccess) {
                    NLogLogger.Error("写入 Web 请求审计日志返回失败，TraceId={TraceId}, CorrelationId={CorrelationId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                        entry.TraceId,
                        entry.CorrelationId,
                        result.ErrorCode,
                        result.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                NLogLogger.Warn("Web 请求审计后台消费收到停止信号，消费循环结束。");
                break;
            }
            catch (Exception ex) {
                NLogLogger.Error(ex, "写入 Web 请求审计日志发生异常，TraceId={TraceId}, CorrelationId={CorrelationId}", entry.TraceId, entry.CorrelationId);
            }
        }
    }
}
