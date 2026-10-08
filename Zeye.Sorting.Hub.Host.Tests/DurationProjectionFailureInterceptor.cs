using Microsoft.EntityFrameworkCore.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在历史投影 SQL 已保存但显式事务未提交时注入故障，验证游标与数据一起回滚。</summary>
public sealed class DurationProjectionFailureInterceptor : SaveChangesInterceptor {
    /// <summary>只使下一次窄投影提交失败。</summary>
    public bool FailNext { get; set; }
    /// <summary>保存后抛错，后台必须从上一次完整批次恢复。</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
        if (FailNext && (eventData.Context?.ChangeTracker.Entries<ParcelDurationFact>().Any() == true
            || eventData.Context?.ChangeTracker.Entries<ParcelDurationCall>().Any() == true)) {
            FailNext = false;
            throw new InvalidOperationException("测试注入：耗时索引 SQL 保存后、事务提交前失败。");
        }
        return ValueTask.FromResult(result);
    }
}
