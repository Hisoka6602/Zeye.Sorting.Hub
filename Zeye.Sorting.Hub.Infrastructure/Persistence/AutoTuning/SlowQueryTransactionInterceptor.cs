using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>单独观测事务开始、提交、回滚及保存点等待，避免提交阻塞藏在请求总耗时里。</summary>
public sealed class SlowQueryTransactionInterceptor(SlowQueryAutoTuningPipeline pipeline, string databaseRole = "business") : DbTransactionInterceptor {
    /// <summary>每个事务当前正在执行的动作；只在动作期间保留单调时间戳。</summary>
    private readonly ConcurrentDictionary<Guid, (string Phase, long Started)> _actions = new();
    /// <summary>动作开始，不把事务从建立到释放的业务生命周期计为数据库等待。</summary>
    private void Starting(Guid id, string phase) {
        _actions[id] = (phase, Stopwatch.GetTimestamp()); pipeline.Started("transaction:" + id.ToString("N"));
    }
    /// <summary>动作结束恰好计量一次；失败优先使用已登记的动作，避免错误文字参与指纹。</summary>
    private void Completed(Guid id, DbConnection? connection, Exception? error = null) {
        if (!_actions.TryRemove(id, out var action)) return;
        pipeline.TransactionCompleted(connection?.GetType().Name ?? "", "transaction:" + id.ToString("N"), action.Phase,
            Stopwatch.GetElapsedTime(action.Started), error, databaseRole);
    }
    /// <inheritdoc />
    public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result) {
        // EF 不提供 BeginTransaction 失败事件，不能登记一个永远无法释放的活动项。
        pipeline.TransactionCompleted(connection.GetType().Name, "transaction:" + eventData.TransactionId.ToString("N"), "BEGIN", eventData.Duration, databaseRole: databaseRole);
        return result;
    }
    /// <inheritdoc />
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default) => ValueTask.FromResult(TransactionStarted(connection, eventData, result));
    /// <inheritdoc />
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) { Starting(eventData.TransactionId, "COMMIT"); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(eventData.TransactionId, "COMMIT"); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Completed(eventData.TransactionId, transaction.Connection);
    /// <inheritdoc />
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData.TransactionId, transaction.Connection); return Task.CompletedTask; }
    /// <inheritdoc />
    public override InterceptionResult TransactionRollingBack(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) { Starting(eventData.TransactionId, "ROLLBACK"); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(eventData.TransactionId, "ROLLBACK"); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Completed(eventData.TransactionId, transaction.Connection);
    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData.TransactionId, transaction.Connection); return Task.CompletedTask; }
    /// <inheritdoc />
    public override InterceptionResult CreatingSavepoint(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) { Starting(eventData.TransactionId, "SAVEPOINT"); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> CreatingSavepointAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(eventData.TransactionId, "SAVEPOINT"); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void CreatedSavepoint(DbTransaction transaction, TransactionEventData eventData) => Completed(eventData.TransactionId, transaction.Connection);
    /// <inheritdoc />
    public override Task CreatedSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData.TransactionId, transaction.Connection); return Task.CompletedTask; }
    /// <inheritdoc />
    public override InterceptionResult RollingBackToSavepoint(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) { Starting(eventData.TransactionId, "ROLLBACK TO SAVEPOINT"); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> RollingBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(eventData.TransactionId, "ROLLBACK TO SAVEPOINT"); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData) => Completed(eventData.TransactionId, transaction.Connection);
    /// <inheritdoc />
    public override Task RolledBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData.TransactionId, transaction.Connection); return Task.CompletedTask; }
    /// <inheritdoc />
    public override InterceptionResult ReleasingSavepoint(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result) { Starting(eventData.TransactionId, "RELEASE SAVEPOINT"); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> ReleasingSavepointAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(eventData.TransactionId, "RELEASE SAVEPOINT"); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void ReleasedSavepoint(DbTransaction transaction, TransactionEventData eventData) => Completed(eventData.TransactionId, transaction.Connection);
    /// <inheritdoc />
    public override Task ReleasedSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default) { Completed(eventData.TransactionId, transaction.Connection); return Task.CompletedTask; }
    /// <inheritdoc />
    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Completed(eventData.TransactionId, transaction.Connection, eventData.Exception);
    /// <inheritdoc />
    public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default) { TransactionFailed(transaction, eventData); return Task.CompletedTask; }
}
