using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>EF Core 命令拦截器：采集慢查询样本</summary>
    public sealed class SlowQueryCommandInterceptor : DbCommandInterceptor {
        /// <summary>
        /// 字段：_pipeline。
        /// </summary>
        private readonly SlowQueryAutoTuningPipeline _pipeline;

        /// <summary>初始化慢查询采集拦截器。</summary>
        public SlowQueryCommandInterceptor(SlowQueryAutoTuningPipeline pipeline) {
            _pipeline = pipeline;
        }

        /// <summary>同步非查询命令执行后采集样本。</summary>
        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) {
            _pipeline.Collect(command.CommandText, eventData.Duration, result);
            return result;
        }

        /// <summary>同步标量命令执行后采集样本。</summary>
        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return result;
        }

        /// <summary>同步读取命令执行后采集样本。</summary>
        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return result;
        }

        /// <summary>异步非查询命令执行后采集样本。</summary>
        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration, result);
            return ValueTask.FromResult(result);
        }

        /// <summary>异步标量命令执行后采集样本。</summary>
        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return ValueTask.FromResult(result);
        }

        /// <summary>异步读取命令执行后采集样本。</summary>
        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return ValueTask.FromResult(result);
        }

        /// <summary>同步命令失败时采集异常样本。</summary>
        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) {
            _pipeline.Collect(command.CommandText, eventData.Duration, exception: eventData.Exception);
        }

        /// <summary>异步命令失败时采集异常样本。</summary>
        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration, exception: eventData.Exception);
            return Task.CompletedTask;
        }
    }
}
