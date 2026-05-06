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
    /// 幂等记录冲突错误消息。
    /// </summary>
    public const string RequestInProgressMessage = "相同幂等请求正在处理中，请稍后重试。";

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

        // 步骤 1：先读取当前幂等键是否已存在记录，命中时按状态决定回放或拒绝。
        var currentRecord = await _idempotencyRepository.GetByKeyAsync(
            sourceSystem,
            operationName,
            businessKey,
            payloadHash,
            cancellationToken);
        if (currentRecord is not null) {
            return await ResolveExistingRecordAsync(currentRecord, sourceSystem, operationName, businessKey, loadExistingAsync, cancellationToken);
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
                    return await ResolveExistingRecordAsync(duplicatedRecord, sourceSystem, operationName, businessKey, loadExistingAsync, cancellationToken);
                }
            }

            Logger.Error(
                "新增幂等记录失败，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, ErrorMessage={ErrorMessage}",
                sourceSystem,
                operationName,
                businessKey,
                addResult.ErrorMessage);
            throw new InvalidOperationException(addResult.ErrorMessage ?? "新增幂等记录失败。");
        }

        try {
            // 步骤 3：仅首次请求进入真实业务执行；成功后把 Pending 切换为 Completed。
            var response = await executeAsync(cancellationToken);
            pendingRecord.MarkCompleted();
            await TryUpdateRecordAsync(pendingRecord, sourceSystem, operationName, businessKey, cancellationToken);
            return (response, false);
        }
        catch (Exception ex) {
            // 步骤 4：真实业务抛错时记录失败状态，保留失败原因，随后继续向上抛出原始异常。
            pendingRecord.MarkFailed(ex.Message);
            await TryUpdateRecordAsync(pendingRecord, sourceSystem, operationName, businessKey, cancellationToken);
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
    /// <param name="loadExistingAsync">读取已有结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果与是否为重放结果。</returns>
    private static async Task<(TResponse Response, bool IsReplay)> ResolveExistingRecordAsync<TResponse>(
        IdempotencyRecord record,
        string sourceSystem,
        string operationName,
        string businessKey,
        Func<CancellationToken, Task<TResponse?>> loadExistingAsync,
        CancellationToken cancellationToken)
        where TResponse : class {
        switch (record.Status) {
            case IdempotencyRecordStatus.Completed:
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
            case IdempotencyRecordStatus.Pending:
                Logger.Warn(
                    "相同幂等请求仍在处理中，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}",
                    sourceSystem,
                    operationName,
                    businessKey);
                throw new InvalidOperationException(RequestInProgressMessage);
            case IdempotencyRecordStatus.Rejected:
            case IdempotencyRecordStatus.Failed:
                var message = string.IsNullOrWhiteSpace(record.FailureMessage)
                    ? "相同幂等请求已被拒绝。"
                    : record.FailureMessage;
                Logger.Warn(
                    "相同幂等请求命中终态记录，SourceSystem={SourceSystem}, OperationName={OperationName}, BusinessKey={BusinessKey}, Status={Status}, Message={Message}",
                    sourceSystem,
                    operationName,
                    businessKey,
                    record.Status,
                    message);
                throw new InvalidOperationException(message);
            default:
                throw new InvalidOperationException("幂等记录状态无效。");
        }
    }

    /// <summary>
    /// 尝试更新幂等记录状态。
    /// </summary>
    /// <param name="record">幂等记录。</param>
    /// <param name="sourceSystem">来源系统。</param>
    /// <param name="operationName">操作名称。</param>
    /// <param name="businessKey">业务键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task TryUpdateRecordAsync(
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
    }
}
