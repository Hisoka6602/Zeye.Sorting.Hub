using System.Data.Common;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>现有原生元数据和会话命令的统一计量入口；EF 命令仍由拦截器计量，避免重复采样。</summary>
internal static class SlowQueryDbOperations {
    /// <summary>连接到诊断管线的弱引用映射，不延长连接寿命。</summary>
    private static readonly ConditionalWeakTable<DbConnection, SlowQueryAutoTuningPipeline> Pipelines = new();
    /// <summary>绑定 EF 或启动期管理连接，不保存连接字符串。</summary>
    internal static void Attach(DbConnection connection, SlowQueryAutoTuningPipeline? pipeline) { if (pipeline is not null) Pipelines.GetValue(connection, _ => pipeline); }
    /// <summary>直接元数据命令遇到调用方已打开的连接时，从正式 EF 拦截器取回同一诊断管线。</summary>
    internal static void Attach(DbContext context) {
        var connection = context.Database.GetDbConnection();
        if (Pipelines.TryGetValue(connection, out _)) return;
        var observers = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors;
        observers?.OfType<SlowQueryCommandInterceptor>().FirstOrDefault()?.AttachConnection(connection);
    }
    /// <summary>查询连接关联的诊断管线。</summary>
    private static SlowQueryAutoTuningPipeline? Find(DbCommand command) => command.Connection is { } connection && Pipelines.TryGetValue(connection, out var pipeline) ? pipeline : null;
    /// <summary>统一同步会话写命令的计量。</summary>
    internal static int ExecuteNonQuery(DbCommand command) {
        var pipeline = Find(command); var start = Stopwatch.GetTimestamp(); var key = Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try { var result = command.ExecuteNonQuery(); pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, result); return result; }
        catch (Exception exception) { pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, error: exception); throw; }
    }
    /// <summary>统一异步元数据写命令的计量。</summary>
    internal static async Task<int> ExecuteNonQueryAsync(DbCommand command, CancellationToken token) {
        var pipeline = Find(command); var start = Stopwatch.GetTimestamp(); var key = Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try { var result = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false); pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, result); return result; }
        catch (Exception exception) { pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, error: exception); throw; }
    }
    /// <summary>统一异步标量元数据和锁命令的计量。</summary>
    internal static async Task<object?> ExecuteScalarAsync(DbCommand command, CancellationToken token) {
        var pipeline = Find(command); var start = Stopwatch.GetTimestamp(); var key = Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try { var result = await command.ExecuteScalarAsync(token).ConfigureAwait(false); pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key); return result; }
        catch (Exception exception) { pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, error: exception); throw; }
    }
    /// <summary>原生元数据读取同样覆盖结果消费阶段。</summary>
    internal static Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CancellationToken token) => ExecuteReaderAsync(command, CommandBehavior.Default, token);
    /// <summary>保留顺序读取大字段的命令行为，不能退化为默认缓冲读取。</summary>
    internal static async Task<DbDataReader> ExecuteReaderAsync(DbCommand command, CommandBehavior behavior, CancellationToken token) {
        var pipeline = Find(command); var start = Stopwatch.GetTimestamp(); var key = Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try {
            var reader = await command.ExecuteReaderAsync(behavior, token).ConfigureAwait(false);
            return pipeline is null ? reader : new SlowQueryDataReader(reader, new(pipeline, command.CommandText,
                Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key));
        }
        catch (Exception exception) { pipeline?.CommandCompleted(command.CommandText, Stopwatch.GetElapsedTime(start), command.Connection?.GetType().Name ?? "", key, error: exception); throw; }
    }
    /// <summary>直接打开管理连接时也采集连接池等待和连接失败。</summary>
    internal static async Task OpenAsync(DbConnection connection, CancellationToken token) {
        Pipelines.TryGetValue(connection, out var pipeline); var start = Stopwatch.GetTimestamp(); var key = "connection:" + Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try { await connection.OpenAsync(token).ConfigureAwait(false); pipeline?.ConnectionCompleted(connection.GetType().Name, key, Stopwatch.GetElapsedTime(start)); }
        catch (Exception exception) { pipeline?.ConnectionCompleted(connection.GetType().Name, key, Stopwatch.GetElapsedTime(start), exception); throw; }
    }
    /// <summary>计量既有原生备份事务开始，不替换提供器事务类型、隔离级别或取消语义。</summary>
    internal static async ValueTask<TTransaction> BeginTransactionAsync<TTransaction>(DbConnection connection, Func<ValueTask<TTransaction>> begin) where TTransaction : DbTransaction {
        Pipelines.TryGetValue(connection, out var pipeline); var start = Stopwatch.GetTimestamp(); var key = "transaction:" + Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try {
            var result = await begin().ConfigureAwait(false);
            pipeline?.TransactionCompleted(connection.GetType().Name, key, "BEGIN", Stopwatch.GetElapsedTime(start)); return result;
        }
        catch (Exception exception) { pipeline?.TransactionCompleted(connection.GetType().Name, key, "BEGIN", Stopwatch.GetElapsedTime(start), exception); throw; }
    }
    /// <summary>备份直接提交事务同样可见，保留原始事务对象及失败传播。</summary>
    internal static async Task CommitAsync(DbTransaction transaction, CancellationToken token) {
        var connection = transaction.Connection; SlowQueryAutoTuningPipeline? pipeline = null;
        if (connection is not null) Pipelines.TryGetValue(connection, out pipeline);
        var start = Stopwatch.GetTimestamp(); var key = "transaction:" + Guid.NewGuid().ToString("N"); pipeline?.Started(key);
        try { await transaction.CommitAsync(token).ConfigureAwait(false); pipeline?.TransactionCompleted(connection?.GetType().Name ?? "", key, "COMMIT", Stopwatch.GetElapsedTime(start)); }
        catch (Exception exception) { pipeline?.TransactionCompleted(connection?.GetType().Name ?? "", key, "COMMIT", Stopwatch.GetElapsedTime(start), exception); throw; }
    }
}
