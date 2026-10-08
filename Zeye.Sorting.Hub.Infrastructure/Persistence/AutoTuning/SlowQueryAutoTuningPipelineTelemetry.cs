using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>执行期间与连接阶段的轻量观测，不在热路径访问数据库或文件。</summary>
public sealed partial class SlowQueryAutoTuningPipeline {
    /// <summary>兼容显式构造拦截器的上下文，不覆盖已绑定的全局存储。</summary>
    internal void AttachProfileStore(SlowQueryProfileStore store) => Interlocked.CompareExchange(ref _profileStore, store, null);
    /// <summary>登记尚未完成的操作。</summary>
    internal void Started(string key) => _profileStore?.Started(key);
    /// <summary>移除已经完成、失败或取消的操作。</summary>
    internal void Finished(string key) => _profileStore?.Finished(key);
    /// <summary>发布命令及请求内累计耗时，不重复统计读取器。</summary>
    internal void CommandCompleted(string sql, TimeSpan elapsed, string provider, string key, int rows = 0, Exception? error = null, string databaseRole = "business") {
        Finished(key); SlowQueryRequestScope.Capture()?.RecordCommand(elapsed, 0);
        Collect(sql, elapsed, rows, error, new() { Provider = provider, CommandId = key, DatabaseRole = databaseRole,
            TraceId = Activity.Current?.TraceId.ToString() ?? SlowQueryRequestScope.Capture()?.TraceId ?? "", SpanId = Activity.Current?.SpanId.ToString() ?? "",
            ExecuteMilliseconds = elapsed.Ticks / (decimal)TimeSpan.TicksPerMillisecond, CommandCount = 1, IsCanceled = error is OperationCanceledException });
    }
    /// <summary>连接打开和失败单独分类，不把连接池等待误称为 SQL 执行。</summary>
    internal void ConnectionCompleted(string provider, string key, TimeSpan elapsed, Exception? error = null) {
        Finished(key); SlowQueryRequestScope.Capture()?.RecordConnection(elapsed);
        Collect("CONNECTION OPEN " + provider, elapsed, exception: error, observation: new() {
            Kind = "connection", Provider = provider, CommandId = key, TraceId = Activity.Current?.TraceId.ToString() ?? SlowQueryRequestScope.Capture()?.TraceId ?? "",
            ConnectionMilliseconds = elapsed.Ticks / (decimal)TimeSpan.TicksPerMillisecond, IsCanceled = error is OperationCanceledException
        });
    }
    /// <summary>事务动作单独分类，避免真实提交和回滚延迟被 SQL 拦截器漏掉。</summary>
    internal void TransactionCompleted(string provider, string key, string phase, TimeSpan elapsed, Exception? error = null, string databaseRole = "business") {
        Finished(key); SlowQueryRequestScope.Capture()?.RecordTransaction(elapsed);
        Collect("TRANSACTION " + phase, elapsed, exception: error, observation: new() {
            Kind = "transaction", Provider = provider, DatabaseRole = databaseRole, CommandId = key,
            TraceId = Activity.Current?.TraceId.ToString() ?? SlowQueryRequestScope.Capture()?.TraceId ?? "",
            ExecuteMilliseconds = elapsed.Ticks / (decimal)TimeSpan.TicksPerMillisecond
        });
    }
}
