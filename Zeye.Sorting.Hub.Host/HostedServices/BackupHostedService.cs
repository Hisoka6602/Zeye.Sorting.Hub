using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 备份治理托管服务。
/// </summary>
public sealed class BackupHostedService : BackgroundService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 备份治理验证服务。
    /// </summary>
    private readonly BackupVerificationService _backupVerificationService;

    /// <summary>
    /// 备份配置监视器。
    /// </summary>
    private readonly IOptionsMonitor<BackupOptions> _optionsMonitor;

    /// <summary>
    /// 初始化备份治理托管服务。
    /// </summary>
    /// <param name="backupVerificationService">备份治理验证服务。</param>
    /// <param name="optionsMonitor">备份配置监视器。</param>
    public BackupHostedService(
        BackupVerificationService backupVerificationService,
        IOptionsMonitor<BackupOptions> optionsMonitor) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    }

    /// <summary>
    /// 执行后台循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("备份治理托管服务已启动，VerificationIntervalMinutes={VerificationIntervalMinutes}", GetEffectiveVerificationIntervalMinutes());
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _backupVerificationService.ExecuteAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(GetEffectiveVerificationIntervalMinutes()), stoppingToken);
            }
            catch (OperationCanceledException) {
                Logger.Info("备份治理托管服务正在停止。");
                break;
            }
            catch (Exception exception) {
                Logger.Error(exception, "备份治理托管服务循环异常，VerificationIntervalMinutes={VerificationIntervalMinutes}", GetEffectiveVerificationIntervalMinutes());
                await Task.Delay(TimeSpan.FromMinutes(GetEffectiveVerificationIntervalMinutes()), stoppingToken);
            }
        }
    }

    /// <summary>
    /// 获取有效轮询间隔。
    /// </summary>
    /// <returns>有效轮询间隔（分钟）。</returns>
    private int GetEffectiveVerificationIntervalMinutes() {
        return Math.Clamp(
            _optionsMonitor.CurrentValue.VerificationIntervalMinutes,
            BackupOptions.MinVerificationIntervalMinutes,
            BackupOptions.MaxVerificationIntervalMinutes);
    }
}
