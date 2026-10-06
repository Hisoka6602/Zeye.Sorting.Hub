using Microsoft.EntityFrameworkCore.Diagnostics;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在真实SQL保存后、事务提交前注入一次失败，验证全部业务变更回滚。</summary>
public sealed class ParcelCommitFailureInterceptor : SaveChangesInterceptor {
    /// <summary>真实业务保存次数，验证同票批次只保存一次。</summary>
    public int ProcessingWrites { get; private set; }
    /// <summary>是否使下一次处理事实保存失败。</summary>
    public bool FailNextProcessingCommit { get; set; }
    /// <summary>使下一次 Fusion 耐久接收在 SQL 保存前失败。</summary>
    public bool FailNextFusionReceipt { get; set; }
    /// <summary>使指定清理批次在 SQL 保存后、事务提交前失败。</summary>
    public int FailCleanupBatchNumber { get; set; }
    /// <summary>本次测试已观察的清理批次数。</summary>
    private int _cleanupBatches;
    /// <summary>接收簿事务写入次数，验证批次不会逐条提交。</summary>
    public int FusionReceiptWrites { get; private set; }
    /// <summary>SQL执行之后、事务提交之前注入接收簿故障。</summary>
    public bool FailNextFusionReceiptAfterSql { get; set; }
    /// <summary>测试故障日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>接收簿写入失败时不能返回存储确认，也不能留下已确认的内存任务。</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (eventData.Context is { } processingContext && processingContext.ChangeTracker.Entries<ParcelProcessingRecord>().Any()) ProcessingWrites++;
        if (eventData.Context is { } receiptContext && receiptContext.ChangeTracker.Entries<Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion.FusionFactReceipt>().Any())
            FusionReceiptWrites++;
        if (FailNextFusionReceipt && eventData.Context is { } context
            && context.ChangeTracker.Entries<Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion.FusionFactReceipt>().Any()) {
            FailNextFusionReceipt = false;
            var exception = new InvalidOperationException("测试注入：Fusion 接收簿保存前失败。");
            Logger.Warn(exception, "测试 Fusion 确认耐久边界");
            throw exception;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>SQL执行完成后抛出故障，由仓储事务执行回滚。</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
        if (FailNextFusionReceiptAfterSql && eventData.Context is { } receiptContext
            && receiptContext.ChangeTracker.Entries<Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion.FusionFactReceipt>().Any()) {
            FailNextFusionReceiptAfterSql = false;
            throw new InvalidOperationException("测试注入：接收簿SQL已执行，事务提交前失败。");
        }
        if (FailCleanupBatchNumber > 0 && eventData.Context is { } cleanupContext && cleanupContext.ChangeTracker.Entries<Zeye.Sorting.Hub.Infrastructure.Persistence.Management.ManagedDocument>().Any(x => x.Entity.Key.StartsWith("parcel-cleanup-batch:", StringComparison.Ordinal))
            && ++_cleanupBatches == FailCleanupBatchNumber) {
            FailCleanupBatchNumber = 0;
            throw new InvalidOperationException("测试注入：清理批次审计保存后提交前失败。");
        }
        if (FailNextProcessingCommit && eventData.Context is { } context && context.ChangeTracker.Entries<ParcelProcessingRecord>().Any()) {
            FailNextProcessingCommit = false;
            var exception = new InvalidOperationException("测试注入：业务SQL已执行，提交前失败。");
            Logger.Warn(exception, "测试事务回滚边界");
            throw exception;
        }
        return ValueTask.FromResult(result);
    }
}
