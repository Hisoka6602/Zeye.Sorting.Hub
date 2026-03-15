using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// EF Core 命令拦截器：采集慢查询样本
    /// </summary>
    public sealed class SlowQueryCommandInterceptor : DbCommandInterceptor {
        private readonly SlowQueryAutoTuningPipeline _pipeline;

        public SlowQueryCommandInterceptor(SlowQueryAutoTuningPipeline pipeline) {
            _pipeline = pipeline;
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) {
            _pipeline.Collect(command.CommandText, eventData.Duration, result);
            return result;
        }

        public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return result;
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration, result);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration);
            return ValueTask.FromResult(result);
        }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) {
            _pipeline.Collect(command.CommandText, eventData.Duration, exception: eventData.Exception);
        }

        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default) {
            _pipeline.Collect(command.CommandText, eventData.Duration, exception: eventData.Exception);
            return Task.CompletedTask;
        }
    }
}
