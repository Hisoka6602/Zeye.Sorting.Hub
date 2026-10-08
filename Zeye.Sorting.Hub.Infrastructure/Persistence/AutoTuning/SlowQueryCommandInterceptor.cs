using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>EF Core 命令拦截器：采集慢查询样本</summary>
    public sealed class SlowQueryCommandInterceptor : DbCommandInterceptor {
        /// <summary>
        /// 自动调优流水线实例，用于采集和分析慢查询样本。
        /// </summary>
        private readonly SlowQueryAutoTuningPipeline _pipeline;
        /// <summary>当前上下文对应数据库用途。</summary>
        private readonly string _databaseRole;

        /// <summary>初始化慢查询采集拦截器。</summary>
        public SlowQueryCommandInterceptor(SlowQueryAutoTuningPipeline pipeline, SlowQueryProfileStore? profileStore = null, string databaseRole = "business") {
            _pipeline = pipeline;
            _databaseRole = databaseRole;
            if (profileStore is not null) _pipeline.AttachProfileStore(profileStore);
        }
        /// <summary>调用方提前打开的连接也能绑定原生命令诊断，不能依赖尚未发生的 EF 打开事件。</summary>
        internal void AttachConnection(DbConnection connection) => SlowQueryDbOperations.Attach(connection, _pipeline);

        /// <summary>执行前登记活动项，完成、失败或取消均移除。</summary>
        private void Started(DbCommand command, CommandEventData data) {
            if (command.Connection is { } connection) SlowQueryDbOperations.Attach(connection, _pipeline);
            _pipeline.Started(data.CommandId.ToString("N"));
        }
        /// <summary>标量、写入或执行失败时发布一次。</summary>
        private void Completed(DbCommand command, CommandEndEventData data, int rows = 0, Exception? error = null) =>
            _pipeline.CommandCompleted(command.CommandText, data.Duration, command.Connection?.GetType().Name ?? "", data.CommandId.ToString("N"), rows, error, _databaseRole);
        /// <summary>读取器执行和消费合并为一次观测，保留各阶段耗时。</summary>
        private DbDataReader Wrap(DbCommand command, CommandExecutedEventData data, DbDataReader reader) =>
            new SlowQueryDataReader(reader, new(_pipeline, command.CommandText, data.Duration, command.Connection?.GetType().Name ?? "", data.CommandId.ToString("N"), _databaseRole));
        /// <inheritdoc />
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Started(command, eventData); return result; }
        /// <inheritdoc />
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Started(command, eventData); return ValueTask.FromResult(result); }
        /// <inheritdoc />
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Started(command, eventData); return result; }
        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Started(command, eventData); return ValueTask.FromResult(result); }
        /// <inheritdoc />
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Started(command, eventData); return result; }
        /// <inheritdoc />
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Started(command, eventData); return ValueTask.FromResult(result); }

        /// <summary>同步非查询命令执行后采集样本。</summary>
        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) {
            Completed(command, eventData, result);
            return result;
        }

        /// <summary>同步标量命令执行后采集样本。</summary>
        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) {
            Completed(command, eventData);
            return result;
        }

        /// <summary>同步读取命令执行后采集样本。</summary>
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
            return Wrap(command, eventData, result);
        }

        /// <summary>异步非查询命令执行后采集样本。</summary>
        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) {
            Completed(command, eventData, result);
            return ValueTask.FromResult(result);
        }

        /// <summary>异步标量命令执行后采集样本。</summary>
        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default) {
            Completed(command, eventData);
            return ValueTask.FromResult(result);
        }

        /// <summary>异步读取命令执行后采集样本。</summary>
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(Wrap(command, eventData, result));
        }

        /// <summary>同步命令失败时采集异常样本。</summary>
        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) {
            Completed(command, eventData, error: eventData.Exception);
        }

        /// <summary>异步命令失败时采集异常样本。</summary>
        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default) {
            Completed(command, eventData, error: eventData.Exception);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void CommandCanceled(DbCommand command, CommandEndEventData eventData) => Completed(command, eventData, error: new OperationCanceledException());
        /// <inheritdoc />
        public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default) {
            Completed(command, eventData, error: new OperationCanceledException(cancellationToken)); return Task.CompletedTask;
        }
    }
}
