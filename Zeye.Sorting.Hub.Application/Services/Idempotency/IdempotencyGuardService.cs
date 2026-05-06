using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Enums.Idempotency;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.Idempotency;

/// <summary>
/// 幂等守卫应用服务。
/// </summary>
public sealed class IdempotencyGuardService {
    /// <summary>
    /// 取消后状态回写的最大补偿等待时间。
    /// </summary>
    private static readonly TimeSpan CancellationPersistenceTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 幂等记录冲突错误消息。
    /// </summary>
    public const string RequestInProgressMessage = "相同幂等请求正在处理中，请稍后重试。";

    /// <summary>
    /// 幂等请求取消后允许重试的提示消息。
    /// </summary>
    public const string RequestCanceledMessage = "幂等请求已取消，可重新发起。";

    /// <summary>
    /// 幂等记录仓储。
    /// </summary>
    private readonly IIdempotencyRepository _idempotencyRepository;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 初始化幂等守卫应用服务。
    /// </summary>
    /// <param name="idempotencyRepository">幂等记录仓储。</param>
    public IdempotencyGuardService(IIdempotencyRepository idempotencyRepository) {
        _idempotencyRepository = idempotencyRepository ?? throw new ArgumentNullException(nameof(idempotencyRepository));
    }

    /// <summary>
    /// 在幂等保护下执行写入操作。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="payloadHash">载荷哈希。</param>
    /// <param name="executeAsync">首次执行委托。</param>
    /// <param name="loadExistingAsync">读取已存在结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    public async Task<(TResponse Response, bool IsReplay)> ExecuteAsync<TResponse>(
        string sourceSystem,
        string operationName,
        string businessKey,
        string payloadHash,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadHash);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(loadExistingAsync);

        // 步骤 1：先读取当前幂等键是否已存在记录；若已存在，则按状态决定回放、拒绝或重试接管。
        var currentRecord = await _idempotencyRepository.GetByKeyAsync(
            sourceSystem,
            operationName,
            businessKey,
            payloadHash,
            cancellationToken);
        if (currentRecord is not null) {
            return await HandleExistingRecordAsync(
                currentRecord,
                sourceSystem,
                operationName,
                businessKey,
                executeAsync,
                loadExistingAsync,
                cancellationToken);
        }

        // 步骤 2：创建 Pending 记录；若并发竞争导致唯一键冲突，则回退到“已存在记录”分支统一处理。
        var pendingRecord = IdempotencyRecord.CreatePending(sourceSystem, operationName, businessKey, payloadHash);
        var addResult = await _idempotencyRepository.AddAsync(pendingRecord, cancellationToken);
        if (!addResult.IsSuccess) {
            if (string.Equals(addResult.ErrorCode, RepositoryErrorCodes.IdempotencyRecordConflict, StringComparison.Ordinal)) {
                var duplicatedRecord = await _idempotencyRepository.GetByKeyAsync(
                    sourceSystem,
                    operationName,
                    businessKey,
                    payloadHash,
                    cancellationToken);
                if (duplicatedRecord is not null) {
                    return await HandleExistingRecordAsync(
                        duplicatedRecord,
                        sourceSystem,
                        operationName,
                        businessKey,
                        executeAsync,
                        loadExistingAsync,
                        cancellationToken);
                }
            }

            Logger.Error(
                "新增幂等记录失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, ErrorMessage={ErrorMessage}",
                sourceSystem,
                operationName,
                businessKey,
                addResult.ErrorMessage);
            throw new IdempotencyGuardException(
                IdempotencyGuardException.StatePersistenceFailedErrorCode,
                addResult.ErrorMessage ?? "新增幂等记录失败。");
        }

        return await ExecuteWithRecordAsync(
            pendingRecord,
            sourceSystem,
            operationName,
            businessKey,
            executeAsync,
            cancellationToken);
    }

    /// <summary>
    /// 使用已持有的幂等记录执行业务。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="record">幂等记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="executeAsync">首次执行委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private async Task<(TResponse Response, bool IsReplay)> ExecuteWithRecordAsync<TResponse>(
        IdempotencyRecord record,
        string sourceSystem,
        string operationName,
        string businessKey,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        try {
            // 步骤 3：仅持有 Pending 记录的请求进入真实业务执行；成功后把记录切换为 Completed。
            var response = await executeAsync(cancellationToken);
            record.MarkCompleted();
            await EnsureRecordStateUpdatedAsync(record, sourceSystem, operationName, businessKey, cancellationToken);
            return (response, false);
        }
        catch (OperationCanceledException ex) {
            // 步骤 4：取消不应写成 Failed；转为 Rejected 以显式允许后续同请求重试。
            Logger.Warn(
                ex,
                "幂等请求执行被取消，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                sourceSystem,
                operationName,
                businessKey);
            record.MarkRejected(RequestCanceledMessage);
            using var persistenceTokenSource = CreateCancellationPersistenceTokenSource(cancellationToken);
            await EnsureRecordStateUpdatedAsync(
                record,
                sourceSystem,
                operationName,
                businessKey,
                persistenceTokenSource.Token);
            throw;
        }
        catch (Exception ex) {
            // 步骤 5：真实业务失败时记录 Failed 状态，保留失败原因，随后继续向上抛出原始异常。
            Logger.Error(
                ex,
                "幂等请求执行失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                sourceSystem,
                operationName,
                businessKey);
            record.MarkFailed(ex.Message);
            await EnsureRecordStateUpdatedAsync(record, sourceSystem, operationName, businessKey, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 处理已存在的幂等记录。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="record">已存在的幂等记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="executeAsync">首次执行委托。</param>
    /// <param name="loadExistingAsync">读取已有结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private async Task<(TResponse Response, bool IsReplay)> HandleExistingRecordAsync<TResponse>(
        IdempotencyRecord record,
        string sourceSystem,
        string operationName,
        string businessKey,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        switch (record.Status) {
            case IdempotencyRecordStatus.Completed:
                return await ReplayExistingResponseAsync(loadExistingAsync, sourceSystem, operationName, businessKey, cancellationToken);
            case IdempotencyRecordStatus.Pending:
                var pendingResponse = await loadExistingAsync(cancellationToken);
                if (pendingResponse is not null) {
                    Logger.Error(
                        "幂等记录仍为 Pending，但已检测到真实业务结果，尝试按重放语义恢复。SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                        sourceSystem,
                        operationName,
                        businessKey);
                    record.MarkCompleted();
                    await TryRepairCompletedStateAsync(record, sourceSystem, operationName, businessKey, cancellationToken);
                    return (pendingResponse, true);
                }

                Logger.Warn(
                    "相同幂等请求仍在处理中，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                    sourceSystem,
                    operationName,
                    businessKey);
                throw new IdempotencyGuardException(
                    IdempotencyGuardException.RequestInProgressErrorCode,
                    RequestInProgressMessage);
            case IdempotencyRecordStatus.Rejected:
                Logger.Warn(
                    "相同幂等请求命中可重试拒绝记录，准备重新接管执行。SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                    sourceSystem,
                    operationName,
                    businessKey);
                record.ReopenPending();
                await EnsureRecordStateUpdatedAsync(record, sourceSystem, operationName, businessKey, cancellationToken);
                return await ExecuteWithRecordAsync(record, sourceSystem, operationName, businessKey, executeAsync, cancellationToken);
            case IdempotencyRecordStatus.Failed:
                var failedMessage = string.IsNullOrWhiteSpace(record.FailureMessage)
                    ? "相同幂等请求已失败。"
                    : record.FailureMessage;
                Logger.Warn(
                    "相同幂等请求命中失败终态记录，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, Message={Message}",
                    sourceSystem,
                    operationName,
                    businessKey,
                    failedMessage);
                throw new InvalidOperationException(failedMessage);
            default:
                throw new InvalidOperationException("幂等记录状态无效。");
        }
    }

    /// <summary>
    /// 回放已存在结果。
    /// </summary>
    /// <typeparam name="TResponse">返回值类型。</typeparam>
    /// <param name="loadExistingAsync">读取已有结果的委托。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private static async Task<(TResponse Response, bool IsReplay)> ReplayExistingResponseAsync<TResponse>(
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        string sourceSystem,
        string operationName,
        string businessKey,
        CancellationToken cancellationToken)
        where TResponse : class {
        var response = await loadExistingAsync(cancellationToken);
        if (response is null) {
            Logger.Error(
                "幂等记录已完成但未能读取已有结果，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                sourceSystem,
                operationName,
                businessKey);
            throw new InvalidOperationException("幂等请求已完成，但未能读取已有结果。");
        }

        return (response, true);
    }

    /// <summary>
    /// 尝试修复已存在业务结果但记录仍为 Pending 的状态。
    /// </summary>
    /// <param name="record">幂等记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task TryRepairCompletedStateAsync(
        IdempotencyRecord record,
        string sourceSystem,
        string operationName,
        string businessKey,
        CancellationToken cancellationToken) {
        var updateResult = await _idempotencyRepository.UpdateAsync(record, cancellationToken);
        if (updateResult.IsSuccess) {
            return;
        }

        Logger.Error(
            "幂等记录恢复为 Completed 失败，将继续按重放语义返回结果。SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, ErrorMessage={ErrorMessage}",
            sourceSystem,
            operationName,
            businessKey,
            updateResult.ErrorMessage);
    }

    /// <summary>
    /// 强制更新幂等记录状态。
    /// </summary>
    /// <param name="record">幂等记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="IdempotencyGuardException">当状态落库失败时抛出。</exception>
    private async Task EnsureRecordStateUpdatedAsync(
        IdempotencyRecord record,
        string sourceSystem,
        string operationName,
        string businessKey,
        CancellationToken cancellationToken) {
        var updateResult = await _idempotencyRepository.UpdateAsync(record, cancellationToken);
        if (updateResult.IsSuccess) {
            return;
        }

        Logger.Error(
            "更新幂等记录失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, Status={Status}, ErrorMessage={ErrorMessage}",
            sourceSystem,
            operationName,
            businessKey,
            record.Status,
            updateResult.ErrorMessage);
        throw new IdempotencyGuardException(
            IdempotencyGuardException.StatePersistenceFailedErrorCode,
            updateResult.ErrorMessage ?? "更新幂等记录失败。");
    }

    /// <summary>
    /// 为取消后的状态回写创建带超时保护的取消令牌源。
    /// </summary>
    /// <param name="cancellationToken">原始取消令牌。</param>
    /// <returns>用于补偿回写的取消令牌源。</returns>
    private static CancellationTokenSource CreateCancellationPersistenceTokenSource(CancellationToken cancellationToken) {
        if (!cancellationToken.IsCancellationRequested) {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        return new CancellationTokenSource(CancellationPersistenceTimeout);
    }
}
