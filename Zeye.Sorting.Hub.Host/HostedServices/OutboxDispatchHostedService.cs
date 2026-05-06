using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using Zeye.Sorting.Hub.Application.Services.Events;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// Outbox 消息派发后台服务。
/// 当前阶段仅执行状态推进与日志派发模拟。
/// </summary>
public sealed class OutboxDispatchHostedService : BackgroundService {
    /// <summary>
    /// 轮询间隔配置键。
    /// </summary>
    private const string PollIntervalSecondsConfigKey = "Outbox:Dispatch:PollIntervalSeconds";

    /// <summary>
    /// 批次大小配置键。
    /// </summary>
    private const string BatchSizeConfigKey = "Outbox:Dispatch:BatchSize";

    /// <summary>
    /// 最大重试次数配置键。
    /// </summary>
    private const string MaxRetryCountConfigKey = "Outbox:Dispatch:MaxRetryCount";

    /// <summary>
    /// 默认轮询间隔。
    /// </summary>
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 默认批次大小。
    /// </summary>
    private const int DefaultBatchSize = 20;

    /// <summary>
    /// 默认最大重试次数。
    /// </summary>
    private const int DefaultMaxRetryCount = 5;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 服务作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 轮询间隔。
    /// </summary>
    private readonly TimeSpan _pollInterval;

    /// <summary>
    /// 单轮批次大小。
    /// </summary>
    private readonly int _batchSize;

    /// <summary>
    /// 最大重试次数。
    /// </summary>
    private readonly int _maxRetryCount;

    /// <summary>
    /// 初始化 Outbox 消息派发后台服务。
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂。</param>
    /// <param name="configuration">配置根。</param>
    public OutboxDispatchHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _pollInterval = ResolvePollInterval(configuration);
        _batchSize = ResolveBatchSize(configuration);
        _maxRetryCount = ResolveMaxRetryCount(configuration);
    }

    /// <summary>
    /// 运行单轮派发。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本轮处理条数。</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken) {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var commandService = scope.ServiceProvider.GetRequiredService<DispatchOutboxMessageCommandService>();
        return await commandService.ExecuteAsync(_batchSize, _maxRetryCount, cancellationToken);
    }

    /// <summary>
    /// 执行后台派发循环。
    /// </summary>
    /// <param name="stoppingToken">取消令牌。</param>
    /// <returns>后台任务。</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            await DispatchAndLogAsync(stoppingToken);

            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken)) {
                await DispatchAndLogAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            NLogLogger.Info("Outbox 消息派发后台服务已停止。");
        }
    }

    /// <summary>
    /// 执行单轮派发并输出摘要日志。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    private async Task DispatchAndLogAsync(CancellationToken cancellationToken) {
        try {
            var handledCount = await RunOnceAsync(cancellationToken);
            if (handledCount > 0) {
                NLogLogger.Info("Outbox 消息派发完成，HandledCount={HandledCount}, BatchSize={BatchSize}, MaxRetryCount={MaxRetryCount}", handledCount, _batchSize, _maxRetryCount);
            }
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "Outbox 消息派发后台服务执行失败。");
        }
    }

    /// <summary>
    /// 解析轮询间隔。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>轮询间隔。</returns>
    private static TimeSpan ResolvePollInterval(IConfiguration configuration) {
        var seconds = configuration.GetValue<int?>(PollIntervalSecondsConfigKey) ?? (int)DefaultPollInterval.TotalSeconds;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 300));
    }

    /// <summary>
    /// 解析批次大小。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>批次大小。</returns>
    private static int ResolveBatchSize(IConfiguration configuration) {
        var batchSize = configuration.GetValue<int?>(BatchSizeConfigKey) ?? DefaultBatchSize;
        return Math.Clamp(batchSize, 1, 200);
    }

    /// <summary>
    /// 解析最大重试次数。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>最大重试次数。</returns>
    private static int ResolveMaxRetryCount(IConfiguration configuration) {
        var maxRetryCount = configuration.GetValue<int?>(MaxRetryCountConfigKey) ?? DefaultMaxRetryCount;
        return Math.Clamp(maxRetryCount, 1, 20);
    }
}
