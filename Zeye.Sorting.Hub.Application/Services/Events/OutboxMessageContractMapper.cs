using Zeye.Sorting.Hub.Contracts.Models.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;

namespace Zeye.Sorting.Hub.Application.Services.Events;

/// <summary>
/// Outbox 消息合同映射器。
/// </summary>
public static class OutboxMessageContractMapper {
    /// <summary>
    /// 将聚合映射为响应合同。
    /// </summary>
    /// <param name="outboxMessage">Outbox 消息聚合。</param>
    /// <returns>响应合同。</returns>
    public static OutboxMessageResponse ToResponse(OutboxMessage outboxMessage) {
        ArgumentNullException.ThrowIfNull(outboxMessage);

        return new OutboxMessageResponse {
            Id = outboxMessage.Id,
            EventType = outboxMessage.EventType,
            PayloadJson = outboxMessage.PayloadJson,
            Status = outboxMessage.Status.ToString(),
            RetryCount = outboxMessage.RetryCount,
            FailureMessage = outboxMessage.FailureMessage,
            CreatedAt = outboxMessage.CreatedAt,
            UpdatedAt = outboxMessage.UpdatedAt,
            LastAttemptedAt = outboxMessage.LastAttemptedAt,
            CompletedAt = outboxMessage.CompletedAt
        };
    }
}
