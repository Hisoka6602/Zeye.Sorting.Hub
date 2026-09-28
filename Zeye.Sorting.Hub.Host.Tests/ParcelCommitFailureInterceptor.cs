using Microsoft.EntityFrameworkCore.Diagnostics;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在真实SQL保存后、事务提交前注入一次失败，验证全部业务变更回滚。</summary>
public sealed class ParcelCommitFailureInterceptor : SaveChangesInterceptor {
    /// <summary>是否使下一次处理事实保存失败。</summary>
    public bool FailNextProcessingCommit { get; set; }
    /// <summary>测试故障日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>SQL执行完成后抛出故障，由仓储事务执行回滚。</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
        if (FailNextProcessingCommit && eventData.Context is { } context && context.ChangeTracker.Entries<ParcelProcessingRecord>().Any()) {
            FailNextProcessingCommit = false;
            var exception = new InvalidOperationException("测试注入：业务SQL已执行，提交前失败。");
            Logger.Warn(exception, "测试事务回滚边界");
            throw exception;
        }
        return ValueTask.FromResult(result);
    }
}
