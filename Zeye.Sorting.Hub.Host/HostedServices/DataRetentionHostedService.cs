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
    /// 数据保留治理执行器。
    /// </summary>
    private readonly DataRetentionExecutor _dataRetentionExecutor;

    /// <summary>
    /// 数据保留治理配置监视器。
    /// </summary>
    private readonly IOptionsMonitor<DataRetentionOptions> _optionsMonitor;

    /// <summary>
    /// 初始化数据保留治理后台服务。
    /// </summary>
    /// <param name="dataRetentionExecutor">数据保留治理执行器。</param>
    /// <param name="optionsMonitor">配置监视器。</param>
    public DataRetentionHostedService(
        DataRetentionExecutor dataRetentionExecutor,
        IOptionsMonitor<DataRetentionOptions> optionsMonitor) {
        _dataRetentionExecutor = dataRetentionExecutor ?? throw new ArgumentNullException(nameof(dataRetentionExecutor));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    }

    /// <summary>
    /// 执行后台循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        Logger.Info("数据保留治理后台服务已启动，ExecutionIntervalMinutes={ExecutionIntervalMinutes}", GetEffectiveExecutionIntervalMinutes());
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await _dataRetentionExecutor.ExecuteAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(GetEffectiveExecutionIntervalMinutes()), stoppingToken);
            }
            catch (OperationCanceledException) {
                Logger.Info("数据保留治理后台服务正在停止。");
                break;
            }
            catch (Exception exception) {
                Logger.Error(exception, "数据保留治理后台服务循环异常，ExecutionIntervalMinutes={ExecutionIntervalMinutes}", GetEffectiveExecutionIntervalMinutes());
                await Task.Delay(TimeSpan.FromMinutes(GetEffectiveExecutionIntervalMinutes()), stoppingToken);
            }
        }
    }

    /// <summary>
    /// 获取有效执行间隔。
    /// </summary>
    /// <returns>有效执行间隔（分钟）。</returns>
    private int GetEffectiveExecutionIntervalMinutes() {
        return Math.Clamp(
            _optionsMonitor.CurrentValue.ExecutionIntervalMinutes,
            DataRetentionOptions.MinExecutionIntervalMinutes,
            DataRetentionOptions.MaxExecutionIntervalMinutes);
    }
}
