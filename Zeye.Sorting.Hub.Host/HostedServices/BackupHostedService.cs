using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Application.Abstractions.Storage;
using System.Diagnostics;

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
    /// <summary>跨平台实际备份执行器。</summary>
    private readonly IDatabaseBackupArtifactService _artifacts;
    /// <summary>共享数据库中的运行策略。</summary>
    private readonly OperationalPolicyService _policy;

    /// <summary>
    /// 初始化备份治理后台服务。
    /// </summary>
    /// <param name="backupVerificationService">备份校验服务。</param>
    /// <param name="options">备份配置。</param>
    /// <param name="artifacts">实际备份执行器。</param>
    /// <param name="policy">运行策略。</param>
    public BackupHostedService(BackupVerificationService backupVerificationService, IOptions<BackupOptions> options, IDatabaseBackupArtifactService artifacts, OperationalPolicyService policy) {
        _backupVerificationService = backupVerificationService ?? throw new ArgumentNullException(nameof(backupVerificationService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _artifacts = artifacts; _policy = policy;
    }

    /// <summary>
    /// 执行后台轮询。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("备份治理后台服务已启动。");
        long? lastBackup = null;
        long? lastCheck = null;
        long? lastMaintenance = null;
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var policy = await _policy.ReadAsync(stoppingToken);
                if (_options.IsEnabled && policy.AutomaticBackups && _artifacts.IsSupported && (!lastBackup.HasValue || Stopwatch.GetElapsedTime(lastBackup.Value) >= TimeSpan.FromMinutes(policy.BackupIntervalMinutes))) {
                    await _artifacts.CreateAsync("后台备份", stoppingToken); lastBackup = Stopwatch.GetTimestamp();
                    await _backupVerificationService.ExecuteAsync(stoppingToken); lastCheck = Stopwatch.GetTimestamp();
                }
                else if (!lastCheck.HasValue || Stopwatch.GetElapsedTime(lastCheck.Value) >= TimeSpan.FromMinutes(_options.PollIntervalMinutes)) {
                    await _backupVerificationService.ExecuteAsync(stoppingToken); lastCheck = Stopwatch.GetTimestamp();
                }
                // 摘要核验需要顺序读盘，限制为每小时维护一次，重启后先复核一次历史目录。
                if (!lastMaintenance.HasValue || Stopwatch.GetElapsedTime(lastMaintenance.Value) >= TimeSpan.FromHours(1)) {
                    lastMaintenance = Stopwatch.GetTimestamp();
                    await _artifacts.MaintainAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("备份治理后台服务收到停止信号。");
                break;
            }
            catch (Exception exception) {
                Logger.Error(exception, "备份治理后台服务执行失败。");
            }

            try {
                await _policy.WaitForChangeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Info("备份治理后台服务延迟等待被取消。");
                break;
            }
        }

        Logger.Info("备份治理后台服务已停止。");
    }
}
