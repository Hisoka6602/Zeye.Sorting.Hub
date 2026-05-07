using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.Events;

/// <summary>
/// Inbox 消息幂等消费守卫应用服务。
/// </summary>
public sealed class InboxMessageGuardService {
    /// <summary>
    /// 取消后状态回写的最大补偿等待时间。
    /// </summary>
    private static readonly TimeSpan CancellationPersistenceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 消息处理中提示。
    /// </summary>
    public const string MessageInProgressMessage = "相同 Inbox 消息正在处理中，请稍后重试。";

    /// <summary>
    /// 消息已过期提示。
    /// </summary>
    public const string MessageExpiredMessage = "Inbox 消息已过期，不允许继续消费。";

    /// <summary>
    /// 消息取消后允许重试的提示。
    /// </summary>
    public const string MessageCanceledMessage = "Inbox 消息消费已取消，可重新发起。";

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Inbox 消息仓储。
    /// </summary>
    private readonly IInboxMessageRepository _inboxMessageRepository;

    /// <summary>
    /// 初始化 Inbox 消息幂等消费守卫应用服务。
    /// </summary>
    /// <param name="inboxMessageRepository">Inbox 消息仓储。</param>
    public InboxMessageGuardService(IInboxMessageRepository inboxMessageRepository) {
        _inboxMessageRepository = inboxMessageRepository ?? throw new ArgumentNullException(nameof(inboxMessageRepository));
    }

    /// <summary>
    /// 在 Inbox 幂等保护下执行消息消费。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="eventType">事件类型。</param>
    /// <param name="expiresAt">过期治理时间。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="executeAsync">首次执行委托。</param>
    /// <param name="loadExistingAsync">读取已有结果委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    public async Task<(TResponse Response, bool IsReplay)> ExecuteAsync<TResponse>(
        string sourceSystem,
        string messageId,
        string eventType,
        DateTime? expiresAt,
        int maxRetryCount,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(loadExistingAsync);
        if (maxRetryCount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "maxRetryCount 必须大于 0。");
        }

        var normalizedSourceSystem = InboxMessage.NormalizeSourceSystem(sourceSystem);
        var normalizedMessageId = InboxMessage.NormalizeMessageId(messageId);
        var normalizedEventType = InboxMessage.NormalizeEventType(eventType);

        // 步骤 1：先读取当前 Inbox 记录，若已存在则按状态决定回放、拒绝或重试接管。
        var currentRecord = await _inboxMessageRepository.GetByKeyAsync(
            normalizedSourceSystem,
            normalizedMessageId,
            cancellationToken);
        if (currentRecord is not null) {
            return await HandleExistingRecordAsync(
                currentRecord,
                normalizedSourceSystem,
                normalizedMessageId,
                maxRetryCount,
                executeAsync,
                loadExistingAsync,
                cancellationToken);
        }

        // 步骤 2：首次消费时先创建 Pending 记录；若并发写入导致唯一键冲突，则回退到“已存在记录”分支统一处理。
        var pendingRecord = InboxMessage.CreatePending(normalizedSourceSystem, normalizedMessageId, normalizedEventType, expiresAt);
        var addResult = await _inboxMessageRepository.AddAsync(pendingRecord, cancellationToken);
        if (!addResult.IsSuccess) {
            if (string.Equals(addResult.ErrorCode, RepositoryErrorCodes.InboxMessageConflict, StringComparison.Ordinal)) {
                var duplicatedRecord = await _inboxMessageRepository.GetByKeyAsync(
                    normalizedSourceSystem,
                    normalizedMessageId,
                    cancellationToken);
                if (duplicatedRecord is not null) {
                    return await HandleExistingRecordAsync(
                        duplicatedRecord,
                        normalizedSourceSystem,
                        normalizedMessageId,
                        maxRetryCount,
                        executeAsync,
                        loadExistingAsync,
                        cancellationToken);
                }
            }

            Logger.Error(
                "新增 Inbox 消息失败，SourceSystem={SourceSystem}, MessageId={MessageId}, ErrorMessage={ErrorMessage}",
                normalizedSourceSystem,
                normalizedMessageId,
                addResult.ErrorMessage);
            throw new InvalidOperationException(addResult.ErrorMessage ?? "新增 Inbox 消息失败。");
        }

        return await ExecuteWithRecordAsync(
            normalizedSourceSystem,
            normalizedMessageId,
            maxRetryCount,
            executeAsync,
            cancellationToken);
    }

    /// <summary>
    /// 使用持有的 Inbox 记录执行业务。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="executeAsync">消费委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private async Task<(TResponse Response, bool IsReplay)> ExecuteWithRecordAsync<TResponse>(
        string sourceSystem,
        string messageId,
        int maxRetryCount,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        var record = await _inboxMessageRepository.TryAcquireForConsumptionAsync(
            sourceSystem,
            messageId,
            maxRetryCount,
            cancellationToken);
        if (record is null) {
            Logger.Warn(
                "Inbox 消息接管失败，可能已被其他执行器处理，SourceSystem={SourceSystem}, MessageId={MessageId}",
                sourceSystem,
                messageId);
            throw new InvalidOperationException(MessageInProgressMessage);
        }

        try {
            // 步骤 3：仅成功拿到 Processing 状态的记录才允许进入真实业务消费。
            // 步骤 4：消费成功后回写 Succeeded，后续重复消息统一走重放分支。
            var response = await executeAsync(cancellationToken);
            record.MarkSucceeded();
            await EnsureRecordStateUpdatedAsync(record, InboxMessageStatus.Processing, sourceSystem, messageId, cancellationToken);
            return (response, false);
        }
        catch (OperationCanceledException exception) {
            Logger.Warn(
                exception,
                "Inbox 消息消费被取消，SourceSystem={SourceSystem}, MessageId={MessageId}",
                sourceSystem,
                messageId);
            record.MarkFailed(MessageCanceledMessage);
            using var persistenceTokenSource = CreateCancellationPersistenceTokenSource(cancellationToken);
            await EnsureRecordStateUpdatedAsync(
                record,
                InboxMessageStatus.Processing,
                sourceSystem,
                messageId,
                persistenceTokenSource.Token);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            // 步骤 5：业务消费失败时回写 Failed + RetryCount，保留失败原因供后续重试与诊断。
            Logger.Error(
                exception,
                "Inbox 消息消费失败，SourceSystem={SourceSystem}, MessageId={MessageId}",
                sourceSystem,
                messageId);
            record.MarkFailed(exception.Message);
            await EnsureRecordStateUpdatedAsync(record, InboxMessageStatus.Processing, sourceSystem, messageId, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 处理已存在的 Inbox 记录。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="record">已存在记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="executeAsync">消费委托。</param>
    /// <param name="loadExistingAsync">读取已有结果委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private async Task<(TResponse Response, bool IsReplay)> HandleExistingRecordAsync<TResponse>(
        InboxMessage record,
        string sourceSystem,
        string messageId,
        int maxRetryCount,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        if (record.IsExpired(DateTime.Now)) {
            Logger.Warn(
                "Inbox 消息已过期，拒绝继续消费，SourceSystem={SourceSystem}, MessageId={MessageId}, Status={Status}",
                sourceSystem,
                messageId,
                record.Status);
            throw new InvalidOperationException(MessageExpiredMessage);
        }

        switch (record.Status) {
            case InboxMessageStatus.Succeeded:
                return await ReplayExistingResponseAsync(loadExistingAsync, sourceSystem, messageId, cancellationToken);
            case InboxMessageStatus.Pending:
            case InboxMessageStatus.Processing:
                var pendingResponse = await loadExistingAsync(cancellationToken);
                if (pendingResponse is not null) {
                    var expectedStatus = record.Status;
                    Logger.Warn(
                        "Inbox 记录仍未进入成功态，但已检测到真实消费结果，尝试修复为成功态。SourceSystem={SourceSystem}, MessageId={MessageId}",
                        sourceSystem,
                        messageId);
                    record.MarkRecoveredAsSucceeded();
                    await TryRepairSucceededStateAsync(record, expectedStatus, sourceSystem, messageId, cancellationToken);
                    return (pendingResponse, true);
                }

                Logger.Warn(
                    "相同 Inbox 消息仍在处理中，SourceSystem={SourceSystem}, MessageId={MessageId}, Status={Status}",
                    sourceSystem,
                    messageId,
                    record.Status);
                throw new InvalidOperationException(MessageInProgressMessage);
            case InboxMessageStatus.Failed:
                if (!record.CanRetry(maxRetryCount)) {
                    var failedMessage = string.IsNullOrWhiteSpace(record.FailureMessage)
                        ? "Inbox 消息已失败且不可重试。"
                        : record.FailureMessage;
                    Logger.Warn(
                        "Inbox 消息已达到重试上限，SourceSystem={SourceSystem}, MessageId={MessageId}, RetryCount={RetryCount}, Message={Message}",
                        sourceSystem,
                        messageId,
                        record.RetryCount,
                        failedMessage);
                    throw new InvalidOperationException(failedMessage);
                }

                Logger.Warn(
                    "Inbox 消息命中失败记录，准备重新接管消费。SourceSystem={SourceSystem}, MessageId={MessageId}, RetryCount={RetryCount}",
                    sourceSystem,
                    messageId,
                    record.RetryCount);
                return await ExecuteWithRecordAsync(sourceSystem, messageId, maxRetryCount, executeAsync, cancellationToken);
            default:
                throw new InvalidOperationException("Inbox 消息状态无效。");
        }
    }

    /// <summary>
    /// 回放已存在的消费结果。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="loadExistingAsync">读取已有结果委托。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>重放结果。</returns>
    private static async Task<(TResponse Response, bool IsReplay)> ReplayExistingResponseAsync<TResponse>(
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        string sourceSystem,
        string messageId,
        CancellationToken cancellationToken)
        where TResponse : class {
        var existingResponse = await loadExistingAsync(cancellationToken);
        if (existingResponse is null) {
            Logger.Error(
                "Inbox 消息已完成消费但未能读取已有结果，SourceSystem={SourceSystem}, MessageId={MessageId}",
                sourceSystem,
                messageId);
            throw new InvalidOperationException(
                $"Inbox 消息已完成消费，但未能读取已存在结果。SourceSystem={sourceSystem}, MessageId={messageId}");
        }

        return (existingResponse, true);
    }

    /// <summary>
    /// 尝试修复已存在业务结果但记录仍为中间态的成功状态。
    /// </summary>
    /// <param name="record">Inbox 记录。</param>
    /// <param name="expectedStatus">期望的原始状态。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task TryRepairSucceededStateAsync(
        InboxMessage record,
        InboxMessageStatus expectedStatus,
        string sourceSystem,
        string messageId,
        CancellationToken cancellationToken) {
        var updateResult = await _inboxMessageRepository.UpdateAsync(record, expectedStatus, cancellationToken);
        if (updateResult.IsSuccess) {
            return;
        }

        Logger.Error(
            "Inbox 记录修复为成功态失败，将继续按重放语义返回结果。SourceSystem={SourceSystem}, MessageId={MessageId}, ExpectedStatus={ExpectedStatus}, ErrorMessage={ErrorMessage}",
            sourceSystem,
            messageId,
            expectedStatus,
            updateResult.ErrorMessage);
    }

    /// <summary>
    /// 确保 Inbox 记录状态成功写回。
    /// </summary>
    /// <param name="record">Inbox 记录。</param>
    /// <param name="expectedStatus">期望的原始状态。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="messageId">消息标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task EnsureRecordStateUpdatedAsync(
        InboxMessage record,
        InboxMessageStatus expectedStatus,
        string sourceSystem,
        string messageId,
        CancellationToken cancellationToken) {
        var updateResult = await _inboxMessageRepository.UpdateAsync(record, expectedStatus, cancellationToken);
        if (updateResult.IsSuccess) {
            return;
        }

        Logger.Error(
            "更新 Inbox 消息状态失败，SourceSystem={SourceSystem}, MessageId={MessageId}, ExpectedStatus={ExpectedStatus}, Status={Status}, ErrorMessage={ErrorMessage}",
            sourceSystem,
            messageId,
            expectedStatus,
            record.Status,
            updateResult.ErrorMessage);
        throw new InvalidOperationException(updateResult.ErrorMessage ?? "更新 Inbox 消息状态失败。");
    }

    /// <summary>
    /// 为取消后的状态回写创建带超时保护的取消令牌源。
    /// </summary>
    /// <param name="cancellationToken">原始取消令牌。</param>
    /// <returns>用于补偿回写的取消令牌源；调用方在使用后必须释放。</returns>
    private static CancellationTokenSource CreateCancellationPersistenceTokenSource(CancellationToken cancellationToken) {
        if (!cancellationToken.IsCancellationRequested) {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        return new CancellationTokenSource(CancellationPersistenceTimeout);
    }
}
