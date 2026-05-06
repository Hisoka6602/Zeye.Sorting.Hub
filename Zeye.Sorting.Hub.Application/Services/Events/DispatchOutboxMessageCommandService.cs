using System.Text.Json;
using NLog;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Events;

/// <summary>
/// Outbox 消息派发应用服务。
/// 当前阶段仅执行状态推进与日志派发模拟，不接入外部消息中间件，
/// 以便先把可靠落库、失败隔离与重试边界打稳，再在后续切片接入真实分发器。
/// </summary>
public sealed class DispatchOutboxMessageCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Outbox 消息仓储。
    /// </summary>
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    /// <summary>
    /// 初始化 Outbox 消息派发应用服务。
    /// </summary>
    /// <param name="outboxMessageRepository">Outbox 消息仓储。</param>
    public DispatchOutboxMessageCommandService(IOutboxMessageRepository outboxMessageRepository) {
        _outboxMessageRepository = outboxMessageRepository ?? throw new ArgumentNullException(nameof(outboxMessageRepository));
    }

    /// <summary>
    /// 执行一批 Outbox 消息派发。
    /// </summary>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本轮实际处理条数。</returns>
    public async Task<int> ExecuteAsync(int batchSize, int maxRetryCount, CancellationToken cancellationToken) {
        if (batchSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "batchSize 必须大于 0。");
        }

        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "maxRetryCount 必须大于 0。");
        }

        var handledCount = 0;
        for (var index = 0; index < batchSize; index++) {
            var outboxMessage = await _outboxMessageRepository.TryAcquireNextDispatchableAsync(maxRetryCount, cancellationToken);
            if (outboxMessage is null) {
                break;
            }

            // 步骤 1：尝试做最小派发模拟，当前阶段仅校验 JSON 并输出日志，不对接外部 MQ。
            // 步骤 2：成功则推进到 Succeeded；失败则推进到 Failed/DeadLettered，保证无人值守场景可恢复。
            // 步骤 3：每条消息的终态都必须回写数据库，避免长期卡在 Processing。
            try {
                using var payloadDocument = JsonDocument.Parse(outboxMessage.PayloadJson);
                Logger.Info(
                    "Outbox 模拟派发成功，MessageId={MessageId}, EventType={EventType}, PayloadKind={PayloadKind}",
                    outboxMessage.Id,
                    outboxMessage.EventType,
                    payloadDocument.RootElement.ValueKind);
                outboxMessage.MarkDispatchSucceeded();
            }
            catch (JsonException exception) {
                Logger.Error(exception, "Outbox 消息载荷解析失败，MessageId={MessageId}, EventType={EventType}", outboxMessage.Id, outboxMessage.EventType);
                outboxMessage.MarkDispatchFailed("Outbox 载荷不是合法 JSON。", maxRetryCount);
            }
            catch (Exception exception) {
                Logger.Error(exception, "Outbox 模拟派发失败，MessageId={MessageId}, EventType={EventType}", outboxMessage.Id, outboxMessage.EventType);
                outboxMessage.MarkDispatchFailed("Outbox 模拟派发失败。", maxRetryCount);
            }

            var updateResult = await _outboxMessageRepository.UpdateAsync(outboxMessage, cancellationToken);
            if (!updateResult.IsSuccess) {
                throw new InvalidOperationException(updateResult.ErrorMessage ?? "更新 Outbox 消息状态失败。");
            }

            handledCount++;
        }

        return handledCount;
    }
}
