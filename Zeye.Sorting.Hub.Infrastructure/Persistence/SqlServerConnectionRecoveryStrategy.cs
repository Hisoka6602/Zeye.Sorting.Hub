using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>读取、事务或连接失败时淘汰失效池，保留 SQL Server 原有重试判断与事务边界。</summary>
public sealed class SqlServerConnectionRecoveryStrategy : SqlServerRetryingExecutionStrategy {
    /// <summary>复用原提供器策略的依赖、最大重试次数和等待预算。</summary>
    public SqlServerConnectionRecoveryStrategy(ExecutionStrategyDependencies dependencies, int maxRetryCount, TimeSpan maxRetryDelay)
        : base(dependencies, maxRetryCount, maxRetryDelay, errorNumbersToAdd: null) { }

    /// <summary>覆盖数据读取及事务失败，连接恢复不扩大默认可重试异常集合。</summary>
    protected override bool ShouldRetryOn(Exception exception) {
        ConnectionPoolRecovery.Recover(Dependencies.CurrentContext.Context, exception);
        return base.ShouldRetryOn(exception);
    }
}
