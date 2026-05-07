using System.Text.Json;
using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Application.Services.Events;

/// <summary>
/// Outbox 消息追加应用服务。
/// </summary>
public sealed class AppendOutboxMessageCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Outbox 消息仓储。
    /// </summary>
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    /// <summary>
    /// 初始化 Outbox 消息追加应用服务。
    /// </summary>
    /// <param name="outboxMessageRepository">Outbox 消息仓储。</param>
    public AppendOutboxMessageCommandService(IOutboxMessageRepository outboxMessageRepository) {
        _outboxMessageRepository = outboxMessageRepository ?? throw new ArgumentNullException(nameof(outboxMessageRepository));
    }

    /// <summary>
    /// 追加一条独立写入的 Outbox 消息。
    /// </summary>
    /// <param name="request">创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建后的响应合同。</returns>
    public async Task<OutboxMessageResponse> ExecuteAsync(OutboxMessageCreateRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEventType = OutboxMessage.NormalizeEventType(request.EventType);
        var normalizedPayloadJson = NormalizePayloadJson(request.PayloadJson);
        var safeEventType = LineBreakNormalizer.ReplaceLineBreaksToSpace(normalizedEventType);
        var outboxMessage = OutboxMessage.CreatePending(normalizedEventType, normalizedPayloadJson);

        try {
            var addResult = await _outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
            if (!addResult.IsSuccess) {
                throw new InvalidOperationException(addResult.ErrorMessage ?? "追加 Outbox 消息失败。");
            }

            return OutboxMessageContractMapper.ToResponse(outboxMessage);
        }
        catch (Exception exception) {
            Logger.Error(exception, "追加 Outbox 消息失败，EventType={EventType}", safeEventType);
            throw;
        }
    }

    /// <summary>
    /// 规范化事件载荷 JSON。
    /// </summary>
    /// <param name="payloadJson">事件载荷 JSON。</param>
    /// <returns>规范化后的 JSON 文本。</returns>
    private static string NormalizePayloadJson(string payloadJson) {
        if (string.IsNullOrWhiteSpace(payloadJson)) {
            throw new ArgumentException("payloadJson 不能为空。", nameof(payloadJson));
        }

        var normalizedPayloadJson = payloadJson.Trim();
        try {
            using var document = JsonDocument.Parse(normalizedPayloadJson);
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception) {
            throw new ArgumentException("payloadJson 必须为合法 JSON。", nameof(payloadJson), exception);
        }
    }
}
