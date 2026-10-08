using Microsoft.EntityFrameworkCore.Storage;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>Oracle 网络失败时清理失效池，保持原有单次执行，由耐久投递处理重放。</summary>
public sealed class OracleConnectionRecoveryStrategy : ExecutionStrategy {
    /// <summary>最大重试次数为零，不扩大 Oracle 原有操作和用户事务的重试语义。</summary>
    public OracleConnectionRecoveryStrategy(ExecutionStrategyDependencies dependencies) : base(dependencies, 0, TimeSpan.Zero) { }

    /// <summary>在结果读取和事务错误离开上下文之前处理失效连接，原异常继续返回。</summary>
    protected override bool ShouldRetryOn(Exception exception) {
        ConnectionPoolRecovery.Recover(Dependencies.CurrentContext.Context, exception);
        return false;
    }
}
