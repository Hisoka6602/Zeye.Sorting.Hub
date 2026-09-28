using NLog;
using Microsoft.Extensions.Options;
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
    /// 审计配置。
    /// </summary>
    private readonly WebRequestAuditLogOptions _options;

    /// <summary>
    /// 构造后台消费服务。
    /// </summary>
    /// <param name="queue">后台队列。</param>
    /// <param name="scopeFactory">服务作用域工厂。</param>
    public WebRequestAuditBackgroundWorkerHostedService(
        WebRequestAuditBackgroundQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<WebRequestAuditLogOptions> options) {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    /// <summary>
    /// 后台消费主循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>异步任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var batch = new List<WebRequestAuditBackgroundEntry>(_options.BackgroundBatchSize);
        while (!stoppingToken.IsCancellationRequested) {
            try {
                if (!await _queue.Reader.WaitToReadAsync(stoppingToken)) {
                    break;
                }

                if (_options.BackgroundBatchDelayMs > 0) {
                    await Task.Delay(_options.BackgroundBatchDelayMs, stoppingToken);
                }

                await FlushAvailableBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                NLogLogger.Warn("Web 请求审计后台消费收到停止信号，消费循环结束。");
                break;
            }
            catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested) {
                // 宿主关闭时，队列或服务作用域可能已先于消费者释放。
                NLogLogger.Warn("Web 请求审计后台消费在宿主释放后安全退出。");
                break;
            }
            catch (Exception ex) {
                NLogLogger.Error(ex, "批量写入 Web 请求审计日志发生异常，Count={Count}", batch.Count);
            }
        }

        using var drainTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            while (_queue.Depth > 0 && await FlushAvailableBatchAsync(batch, drainTimeout.Token) > 0) {
            }
        }
        catch (OperationCanceledException) when (drainTimeout.IsCancellationRequested) {
            NLogLogger.Warn("Web 请求审计后台队列优雅排空超时，RemainingDepth={RemainingDepth}", _queue.Depth);
        }
        catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested) {
            NLogLogger.Warn("Web 请求审计后台队列因宿主释放停止排空，RemainingDepth={RemainingDepth}", _queue.Depth);
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "Web 请求审计后台队列优雅排空失败，RemainingDepth={RemainingDepth}", _queue.Depth);
        }
    }

    /// <summary>
    /// 从队列读取并写入一个可用批次。
    /// </summary>
    /// <param name="batch">复用的批次缓冲区。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本轮读取的数量。</returns>
    private async Task<int> FlushAvailableBatchAsync(
        List<WebRequestAuditBackgroundEntry> batch,
        CancellationToken cancellationToken) {
        batch.Clear();
        while (batch.Count < _options.BackgroundBatchSize && _queue.Reader.TryRead(out var entry)) {
            _queue.MarkDequeued();
            batch.Add(entry);
        }

        if (batch.Count == 0) {
            return 0;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var writeService = scope.ServiceProvider.GetRequiredService<WriteWebRequestAuditLogCommandService>();
        var logs = batch.Select(static entry => entry.Log).ToArray();
        var result = await writeService.WriteBatchAsync(logs, cancellationToken);
        if (!result.IsSuccess) {
            NLogLogger.Error(
                "批量写入 Web 请求审计日志返回失败，Count={Count}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                batch.Count,
                result.ErrorCode,
                result.ErrorMessage);
        }

        return batch.Count;
    }
}
