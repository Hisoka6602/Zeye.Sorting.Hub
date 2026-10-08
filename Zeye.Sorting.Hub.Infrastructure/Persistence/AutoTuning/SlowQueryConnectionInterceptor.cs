using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>跨提供器采集打开连接的等待与失败，关联直接 ADO 元数据命令。</summary>
public sealed class SlowQueryConnectionInterceptor : DbConnectionInterceptor {
    /// <summary>共享诊断管线。</summary>
    private readonly SlowQueryAutoTuningPipeline _pipeline;
    /// <summary>构造连接观测器。</summary>
    public SlowQueryConnectionInterceptor(SlowQueryAutoTuningPipeline pipeline) => _pipeline = pipeline;
    /// <summary>登记活动连接并绑定直接 ADO 命令的观测。</summary>
    private void Starting(DbConnection connection, ConnectionEventData data) {
        SlowQueryDbOperations.Attach(connection, _pipeline); _pipeline.Started("connection:" + data.ConnectionId.ToString("N"));
    }
    /// <inheritdoc />
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) { Starting(connection, eventData); return result; }
    /// <inheritdoc />
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) { Starting(connection, eventData); return ValueTask.FromResult(result); }
    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => _pipeline.ConnectionCompleted(connection.GetType().Name, "connection:" + eventData.ConnectionId.ToString("N"), eventData.Duration);
    /// <inheritdoc />
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) { ConnectionOpened(connection, eventData); return Task.CompletedTask; }
    /// <inheritdoc />
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData) => _pipeline.ConnectionCompleted(connection.GetType().Name, "connection:" + eventData.ConnectionId.ToString("N"), eventData.Duration, eventData.Exception);
    /// <inheritdoc />
    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default) { ConnectionFailed(connection, eventData); return Task.CompletedTask; }
}
