using Microsoft.EntityFrameworkCore.Storage;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>启用 EF 事务重试守卫；故障注入由客户端显式补传以验证耐久边界。</summary>
internal sealed class FusionTransactionGuardStrategy(ExecutionStrategyDependencies dependencies)
    : ExecutionStrategy(dependencies, 2, TimeSpan.FromMilliseconds(1)) {
    /// <summary>本回归不把提交前注入的非瞬态故障吞掉。</summary>
    protected override bool ShouldRetryOn(Exception exception) => false;
}
