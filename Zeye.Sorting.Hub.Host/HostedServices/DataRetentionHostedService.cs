using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 数据保留治理后台服务。
/// </summary>
public sealed class DataRetentionHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据保留执行器。
    /// </summary>
    private readonly DataRetentionExecutor _executor;

    /// <summary>
    /// 数据保留配置。
    /// </summary>
    private readonly DataRetentionOptions _options;

    /// <summary>
    /// 初始化数据保留治理后台服务。
    /// </summary>
    /// <param name="executor">数据保留执行器。</param>
    /// <param name="options">数据保留配置。</param>
    public DataRetentionHostedService(DataRetentionExecutor executor, IOptions<DataRetentionOptions> options) {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 执行后台轮询。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("数据保留治理后台服务已启动。");
        var pollInterval = TimeSpan.FromMinutes(_options.PollIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _executor.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("数据保留治理后台服务收到停止信号。");
                break;
            }
            catch (Exception exception) {
                Logger.Error(exception, "数据保留治理后台服务执行失败。");
            }

            try {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("数据保留治理后台服务延迟等待被取消。");
                break;
            }
        }

        Logger.Info("数据保留治理后台服务已停止。");
    }
}
