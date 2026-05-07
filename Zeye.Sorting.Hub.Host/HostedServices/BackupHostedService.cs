using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 备份治理后台服务。
/// </summary>
public sealed class BackupHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 备份校验服务。
    /// </summary>
    private readonly BackupVerificationService _backupVerificationService;

    /// <summary>
    /// 备份配置。
    /// </summary>
    private readonly BackupOptions _options;

    /// <summary>
    /// 初始化备份治理后台服务。
    /// </summary>
    /// <param name="backupVerificationService">备份校验服务。</param>
    /// <param name="options">备份配置。</param>
    public BackupHostedService(BackupVerificationService backupVerificationService, IOptions<BackupOptions> options) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 执行后台轮询。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("备份治理后台服务已启动。");
        var pollInterval = TimeSpan.FromMinutes(_options.PollIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _backupVerificationService.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("备份治理后台服务收到停止信号。");
                break;
            }
            catch (Exception exception) {
                Logger.Error(exception, "备份治理后台服务执行失败。");
            }

            try {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("备份治理后台服务延迟等待被取消。");
                break;
            }
        }

        Logger.Info("备份治理后台服务已停止。");
    }
}
