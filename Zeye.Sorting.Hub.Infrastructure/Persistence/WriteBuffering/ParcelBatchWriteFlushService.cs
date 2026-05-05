using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using System.Diagnostics;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// Parcel 批量缓冲写入刷新服务。
/// </summary>
public sealed class ParcelBatchWriteFlushService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 有界写入通道。
    /// </summary>
    private readonly BoundedWriteChannel<BufferedParcelWriteItem> _writeChannel;

    /// <summary>
    /// 死信存储。
    /// </summary>
    private readonly DeadLetterWriteStore _deadLetterWriteStore;

    /// <summary>
    /// 服务作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 缓冲写入配置。
    /// </summary>
    private readonly BufferedWriteOptions _options;

    /// <summary>
    /// 最近一次成功刷新时间（本地时间）。
    /// </summary>
    private DateTime? _lastSuccessfulFlushAtLocal;

    /// <summary>
    /// 最近一次失败刷新时间（本地时间）。
    /// </summary>
    private DateTime? _lastFailedFlushAtLocal;

    /// <summary>
    /// 最近一次失败消息。
    /// </summary>
    private string? _lastFailureMessage;

    /// <summary>
    /// 累计成功刷新批次数。
    /// </summary>
    private long _successfulFlushCount;

    /// <summary>
    /// 累计失败刷新批次数。
    /// </summary>
    private long _failedFlushCount;

    /// <summary>
    /// 累计成功落库记录数。
    /// </summary>
    private long _totalFlushedCount;

    /// <summary>
    /// 初始化 Parcel 批量缓冲写入刷新服务。
    /// </summary>
    /// <param name="writeChannel">有界写入通道。</param>
    /// <param name="deadLetterWriteStore">死信存储。</param>
    /// <param name="serviceScopeFactory">服务作用域工厂。</param>
    /// <param name="options">缓冲写入配置。</param>
    public ParcelBatchWriteFlushService(
        BoundedWriteChannel<BufferedParcelWriteItem> writeChannel,
        DeadLetterWriteStore deadLetterWriteStore,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<BufferedWriteOptions> options) {
        _writeChannel = writeChannel;
        _deadLetterWriteStore = deadLetterWriteStore;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    /// <summary>
    /// 后台刷新主循环。
    /// </summary>
    /// <param name="stoppingToken">停止令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task ProcessAsync(CancellationToken stoppingToken) {
        if (!_options.IsEnabled) {
            Logger.Warn("Parcel 批量缓冲写入未启用，后台刷新服务保持空转退出。");
            return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var hasFlushed = await FlushOnceAsync(stoppingToken);
                if (!hasFlushed) {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                Logger.Warn("Parcel 批量缓冲写入后台刷新服务收到停止信号。QueueDepth={QueueDepth}", _writeChannel.Depth);
                break;
            }
            catch (Exception ex) {
                Logger.Error(ex, "Parcel 批量缓冲写入后台刷新服务发生异常。QueueDepth={QueueDepth}", _writeChannel.Depth);
            }
        }
    }

    /// <summary>
    /// 刷新一个批次。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功消费到批次时返回 true。</returns>
    public async Task<bool> FlushOnceAsync(CancellationToken cancellationToken) {
        if (!await _writeChannel.WaitToReadAsync(cancellationToken)) {
            return false;
        }

        if (!_writeChannel.TryDequeue(out var firstItem)) {
            return false;
        }

        // 步骤 1：先读取第一条，再在刷新窗口内尽量聚合更多记录，降低 SaveChangesAsync 次数。
        var batch = new List<BufferedParcelWriteItem>(_options.BatchSize) { firstItem };
        await FillBatchAsync(batch, cancellationToken);

        // 步骤 2：统一按批次落库；失败时不逐条直写数据库，而是重试或转死信。
        await FlushBatchAsync(batch, cancellationToken);
        return true;
    }

    /// <summary>
    /// 获取当前运行时指标快照。
    /// </summary>
    /// <returns>运行时指标快照。</returns>
    public BatchWriteMetricsSnapshot GetMetricsSnapshot() {
        return new BatchWriteMetricsSnapshot {
            IsEnabled = _options.IsEnabled,
            QueueDepth = _writeChannel.Depth,
            DeadLetterCount = _deadLetterWriteStore.Count,
            DroppedCount = _writeChannel.DroppedCount,
            LastSuccessfulFlushAtLocal = _lastSuccessfulFlushAtLocal,
            LastFailedFlushAtLocal = _lastFailedFlushAtLocal,
            LastFailureMessage = _lastFailureMessage,
            SuccessfulFlushCount = Interlocked.Read(ref _successfulFlushCount),
            FailedFlushCount = Interlocked.Read(ref _failedFlushCount),
            TotalFlushedCount = Interlocked.Read(ref _totalFlushedCount),
            IsBackpressureTriggered = _writeChannel.Depth >= _options.BackpressureRejectThreshold
        };
    }

    /// <summary>
    /// 在刷新窗口内尽量填满批次。
    /// </summary>
    /// <param name="batch">当前批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task FillBatchAsync(List<BufferedParcelWriteItem> batch, CancellationToken cancellationToken) {
        var batchCollectStartTimestamp = Stopwatch.GetTimestamp();
        var flushWindow = TimeSpan.FromMilliseconds(_options.FlushIntervalMilliseconds);
        while (batch.Count < _options.BatchSize) {
            if (_writeChannel.TryDequeue(out var bufferedItem)) {
                batch.Add(bufferedItem);
                continue;
            }

            var remainingTime = flushWindow - Stopwatch.GetElapsedTime(batchCollectStartTimestamp);
            if (remainingTime <= TimeSpan.Zero) {
                break;
            }

            var waitToReadTask = _writeChannel.WaitToReadAsync(cancellationToken).AsTask();
            var delayTask = Task.Delay(remainingTime, cancellationToken);
            var completedTask = await Task.WhenAny(waitToReadTask, delayTask);
            if (completedTask == delayTask) {
                break;
            }

            if (!await waitToReadTask) {
                break;
            }
        }
    }

    /// <summary>
    /// 执行批量落库。
    /// </summary>
    /// <param name="batch">待落库批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task FlushBatchAsync(List<BufferedParcelWriteItem> batch, CancellationToken cancellationToken) {
        using var scope = _serviceScopeFactory.CreateScope();
        var parcelRepository = scope.ServiceProvider.GetRequiredService<IParcelRepository>();
        var parcels = batch.Select(static item => item.Parcel).ToArray();
        RepositoryResult repositoryResult;
        try {
            repositoryResult = await parcelRepository.AddRangeAsync(parcels, cancellationToken);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
            var cancellationMessage = "批量新增取消，失败批次已进入死信隔离。";
            Logger.Warn(ex, "Parcel 批量缓冲写入取消。BatchCount={BatchCount}", batch.Count);
            RecordFailedFlush(batch.Count, cancellationMessage);
            MoveBatchToDeadLetter(batch, cancellationMessage);
            throw;
        }

        if (repositoryResult.IsSuccess) {
            _lastSuccessfulFlushAtLocal = DateTime.Now;
            Interlocked.Increment(ref _successfulFlushCount);
            Interlocked.Add(ref _totalFlushedCount, batch.Count);
            return;
        }

        var failureMessage = repositoryResult.ErrorMessage ?? "批量新增失败。";
        RecordFailedFlush(batch.Count, failureMessage);
        if (cancellationToken.IsCancellationRequested) {
            MoveBatchToDeadLetter(batch, $"{failureMessage}；取消期间进入死信隔离。");
            throw new OperationCanceledException(cancellationToken);
        }

        HandleFailedBatch(batch, failureMessage);
    }

    /// <summary>
    /// 记录失败批次指标与异常日志。
    /// </summary>
    /// <param name="itemCount">批次内记录数量。</param>
    /// <param name="errorMessage">失败消息。</param>
    private void RecordFailedFlush(int itemCount, string errorMessage) {
        _lastFailedFlushAtLocal = DateTime.Now;
        _lastFailureMessage = errorMessage;
        Interlocked.Increment(ref _failedFlushCount);
        Logger.Error(
            "Parcel 批量缓冲写入失败。BatchCount={BatchCount}, ErrorMessage={ErrorMessage}",
            itemCount,
            _lastFailureMessage);
    }

    /// <summary>
    /// 处理失败批次。
    /// </summary>
    /// <param name="batch">失败批次。</param>
    /// <param name="errorMessage">失败消息。</param>
    private void HandleFailedBatch(
        List<BufferedParcelWriteItem> batch,
        string errorMessage) {
        // 步骤 1：优先重试；超过最大重试次数后转死信，确保失败项可观测且不阻塞后续批次。
        foreach (var batchItem in batch) {
            var nextRetryCount = batchItem.RetryCount + 1;
            if (nextRetryCount > _options.MaxRetryCount) {
                _deadLetterWriteStore.Add(new DeadLetterWriteEntry(
                    Parcel: batchItem.Parcel,
                    FailedAtLocal: DateTime.Now,
                    RetryCount: batchItem.RetryCount,
                    ErrorMessage: errorMessage,
                    LastRetryAtLocal: batchItem.LastRetryAtLocal));
                continue;
            }

            var retryItem = batchItem with {
                RetryCount = nextRetryCount,
                LastErrorMessage = errorMessage,
                LastRetryAtLocal = DateTime.Now
            };
            if (_writeChannel.TryEnqueue(retryItem)) {
                continue;
            }

            _deadLetterWriteStore.Add(new DeadLetterWriteEntry(
                Parcel: batchItem.Parcel,
                FailedAtLocal: DateTime.Now,
                RetryCount: nextRetryCount,
                ErrorMessage: $"{errorMessage}；重试回写队列失败。",
                LastRetryAtLocal: retryItem.LastRetryAtLocal));
        }
    }

    /// <summary>
    /// 将批次整体写入死信存储。
    /// </summary>
    /// <param name="batch">失败批次。</param>
    /// <param name="errorMessage">失败消息。</param>
    private void MoveBatchToDeadLetter(List<BufferedParcelWriteItem> batch, string errorMessage) {
        foreach (var batchItem in batch) {
            _deadLetterWriteStore.Add(new DeadLetterWriteEntry(
                Parcel: batchItem.Parcel,
                FailedAtLocal: DateTime.Now,
                RetryCount: batchItem.RetryCount,
                ErrorMessage: errorMessage,
                LastRetryAtLocal: batchItem.LastRetryAtLocal));
        }
    }
}
