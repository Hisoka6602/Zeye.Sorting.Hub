using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 基线数据校验后台服务。
/// </summary>
public sealed class BaselineDataValidationHostedService : IHostedService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 基线校验器。
    /// </summary>
    private readonly BaselineDataValidator _baselineDataValidator;

    /// <summary>
    /// 种子入口。
    /// </summary>
    private readonly BaselineDataSeeder _baselineDataSeeder;

    /// <summary>
    /// 配置选项。
    /// </summary>
    private readonly IOptions<BaselineDataOptions> _baselineDataOptions;

    /// <summary>
    /// 初始化基线数据校验后台服务。
    /// </summary>
    /// <param name="baselineDataValidator">基线校验器。</param>
    /// <param name="baselineDataSeeder">种子入口。</param>
    /// <param name="baselineDataOptions">配置选项。</param>
    public BaselineDataValidationHostedService(
        BaselineDataValidator baselineDataValidator,
        BaselineDataSeeder baselineDataSeeder,
        IOptions<BaselineDataOptions> baselineDataOptions) {
        _baselineDataValidator = baselineDataValidator;
        _baselineDataSeeder = baselineDataSeeder;
        _baselineDataOptions = baselineDataOptions;
    }

    /// <summary>
    /// 启动基线校验。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task StartAsync(CancellationToken cancellationToken) {
        var options = _baselineDataOptions.Value;

        try {
            if (!options.IsValidationEnabled) {
                var disabledResult = BaselineDataValidationResult.CreateDisabled(options);
                _baselineDataValidator.SetLatestResult(disabledResult);
                NLogLogger.Info("基线数据校验未启用。");
                return;
            }

            var validationResult = await _baselineDataValidator.ValidateAsync(cancellationToken);
            var finalResult = validationResult;
            if (validationResult.IsValid && options.IsSeedEnabled) {
                finalResult = await _baselineDataSeeder.SeedAsync(validationResult, cancellationToken);
                _baselineDataValidator.SetLatestResult(finalResult);
            }

            foreach (var warning in finalResult.Warnings) {
                NLogLogger.Warn("基线数据校验提示：{Warning}", warning);
            }

            if (finalResult.IsValid) {
                NLogLogger.Info("基线数据校验完成：{Summary}", finalResult.Summary);
                return;
            }

            NLogLogger.Error("基线数据校验失败：{Summary}，Errors={Errors}", finalResult.Summary, string.Join(" | ", finalResult.Errors));
            if (finalResult.ShouldBlockStartup) {
                throw new InvalidOperationException(finalResult.Summary);
            }
        }
        catch (OperationCanceledException) {
            NLogLogger.Info("基线数据校验已取消。");
        }
        catch (Exception ex) {
            var failedResult = BaselineDataValidationResult.CreateFailed(options, ex.Message);
            _baselineDataValidator.SetLatestResult(failedResult);
            NLogLogger.Error(ex, "基线数据校验执行异常。");
            if (failedResult.ShouldBlockStartup) {
                throw;
            }
        }
    }

    /// <summary>
    /// 停止后台服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}
