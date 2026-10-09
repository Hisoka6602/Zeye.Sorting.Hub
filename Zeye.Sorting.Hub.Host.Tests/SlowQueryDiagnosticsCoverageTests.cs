using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OptionValues = Microsoft.Extensions.Options.Options;
using Zeye.Sorting.Hub.Application.Services.Diagnostics;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Logging;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证执行后流式读取、错误、取消、连接、累计请求和归档恢复的实际采集覆盖。</summary>
public sealed class SlowQueryDiagnosticsCoverageTests : IDisposable {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedMissingTableValues = new[] { "missing_table" };

    /// <summary>测试专用临时目录根，不接触本地业务数据库。</summary>
    private readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "zeye-slow-query-tests"));
    /// <summary>本测试实例专用目录。</summary>
    private readonly string _directory;
    /// <summary>创建与其他测试隔离的目标路径。</summary>
    public SlowQueryDiagnosticsCoverageTests() => _directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));

    /// <summary>同步与异步交叉释放也只关闭底层流一次，释放异常原样返回且观测恰好结束一次。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StreamDisposalForwardsOnceAndPreservesFailures(bool asyncFirst, bool fail) {
        var (_, _, pipeline) = Build();
        using var scope = SlowQueryRequestScope.Begin("stream-disposal");
        var failure = fail ? new IOException("disposal-failure") : null;
        var inner = new DisposeTrackingStream(failure);
        var execution = new SlowQueryExecution(pipeline, "SELECT Payload", TimeSpan.FromTicks(1), "", "stream-disposal");
        var stream = new SlowQueryReaderStream(inner, execution);
        var caught = asyncFirst ? await Record.ExceptionAsync(async () => await stream.DisposeAsync()) : Record.Exception(stream.Dispose);
        Assert.Same(failure, caught);
        stream.Dispose();
        await stream.DisposeAsync();
        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(asyncFirst ? 1 : 0, inner.AsyncDisposeCount);
        execution.Complete();
        Assert.Equal(1, scope.Snapshot("stream-disposal").CommandCount);
    }

    /// <summary>文本大字段释放始终走完整基类链，重入不重复释放或覆盖错误。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextReaderDisposalForwardsOnceAndPreservesFailures(bool fail) {
        var (_, _, pipeline) = Build();
        using var scope = SlowQueryRequestScope.Begin("text-disposal");
        var failure = fail ? new IOException("text-disposal-failure") : null;
        var inner = new DisposeTrackingTextReader(failure);
        var execution = new SlowQueryExecution(pipeline, "SELECT Payload", TimeSpan.FromTicks(1), "", "text-disposal");
        var reader = new SlowQueryTextReader(inner, execution);
        Assert.Same(failure, Record.Exception(reader.Dispose));
        reader.Dispose();
        Assert.Equal(1, inner.DisposeCount);
        execution.Complete();
        Assert.Equal(1, scope.Snapshot("text-disposal").CommandCount);
    }

    /// <summary>真实 EF SQLite 流式读取：第一行返回较快，后续逐行读取仍必须命中慢阈值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingRowsAreMeasuredUntilDisposed(bool async) {
        var (_, profiles, pipeline) = Build(300);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction<long, long>("delayed", value => { Thread.Sleep(80); return value; });
        await using var db = Database(connection, pipeline, profiles);
        using var scope = SlowQueryRequestScope.Begin("streaming-test");
        var query = db.Database.SqlQueryRaw<long>("SELECT delayed(value) AS Value FROM json_each('[1,2,3,4,5]')");
        var values = async ? await query.ToListAsync() : query.ToList();
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, values);
        var profile = Assert.Single(profiles.GetTopProfiles().Items, item => item.Kind == "query");
        Assert.True(profile.AverageReadMilliseconds >= 200m);
        Assert.True(profile.AverageElapsedMilliseconds >= 300m);
        Assert.Equal(5, profile.TotalRowsRead);
        Assert.Equal("streaming-test", profile.TraceId);
        Assert.Equal(1, profile.CallCount);
        Assert.Equal(1, scope.Snapshot("streaming-test").CommandCount);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
    }

    /// <summary>提供器在返回读取器后失败，短于慢阈值也要采集，并且释放不得重复计数。</summary>
    [Fact]
    public async Task ReadFailureAfterExecuteIsCapturedOnce() {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        connection.CreateFunction<long, long>("fail_later", value => value == 2 ? throw new InvalidOperationException("模拟读取故障") : value);
        await using var db = Database(connection, pipeline, profiles);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.SqlQueryRaw<long>(
            "SELECT fail_later(value) AS Value FROM json_each('[1,2,3]')").ToListAsync());
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal(1, profile.CallCount);
        Assert.Equal(1, profile.ErrorCount);
        Assert.Equal(1, profile.PartialReadCount);
        Assert.Contains("SqliteException", profile.ExceptionType, StringComparison.Ordinal);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
    }

    /// <summary>结果读取取消必须立即采集，原始取消继续抛出，释放恰好结束一次。</summary>
    [Fact]
    public async Task ReadingCancellationIsVisibleWithoutWaitingForThreshold() {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        SlowQueryDbOperations.Attach(connection, pipeline);
        await SlowQueryDbOperations.OpenAsync(connection, default);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT 1";
        await using var reader = await SlowQueryDbOperations.ExecuteReaderAsync(command, default);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cancel.Token));
        await reader.DisposeAsync();
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal(1, profile.CallCount);
        Assert.Equal(1, profile.CanceledCount);
        Assert.Equal(0, profile.ErrorCount);
        Assert.Equal(1, profile.PartialReadCount);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
        Assert.Empty(pipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>消费方延迟显示为消费耗时，不据此自动创建数据库索引。</summary>
    [Fact]
    public async Task ConsumerDelayIsSeparatedFromProviderTime() {
        var (_, profiles, pipeline) = Build(100);
        var table = new DataTable(); table.Columns.Add("Value", typeof(long)); table.Rows.Add(1L);
        using var reader = new SlowQueryDataReader(table.CreateDataReader(), new(pipeline, "SELECT Value FROM parcels", TimeSpan.FromTicks(1), "", "consumer"));
        Assert.True(reader.Read());
        await Task.Delay(150);
        Assert.False(reader.Read()); reader.Close(); reader.Dispose();
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.True(profile.AverageConsumerMilliseconds >= 100m);
        Assert.True(profile.AverageElapsedMilliseconds > profile.AverageExecuteMilliseconds + profile.AverageReadMilliseconds);
        Assert.Equal(0, profile.PartialReadCount);
        Assert.Equal(1, profile.CallCount);
        Assert.Empty(pipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>大字段读取期间的错误和取消保留原始异常，不能被读取器已经返回的阶段掩盖。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeFieldFailuresKeepOriginalExceptions(bool cancel) {
        var (_, profiles, pipeline) = Build(600000);
        Exception failure = cancel ? new OperationCanceledException("模拟取消") : new TimeoutException("模拟大字段超时");
        var execution = new SlowQueryExecution(pipeline, "SELECT Payload FROM parcels", TimeSpan.FromTicks(1), "", "large-field");
        using var stream = new SlowQueryReaderStream(new SlowQueryFailingStream(failure), execution);
        var caught = await Record.ExceptionAsync(async () => { await stream.ReadExactlyAsync(new byte[10]); });
        Assert.Same(failure, caught);
        execution.Complete();
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal(1, profile.CallCount);
        Assert.Equal(cancel ? 1 : 0, profile.CanceledCount);
        Assert.Equal(cancel ? 0 : 1, profile.TimeoutCount);
    }

    /// <summary>原生写入、标量与读取分别计量，但必须沿用同一连接和原有结果。</summary>
    [Fact]
    public async Task NativeCommandsAndRequestCountsArePreserved() {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        SlowQueryDbOperations.Attach(connection, pipeline);
        await SlowQueryDbOperations.OpenAsync(connection, default);
        using var scope = SlowQueryRequestScope.Begin("native");
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Test (Value INTEGER)";
        await SlowQueryDbOperations.ExecuteNonQueryAsync(command, default);
        command.CommandText = "INSERT INTO Test VALUES (3)"; Assert.Equal(1, SlowQueryDbOperations.ExecuteNonQuery(command));
        command.CommandText = "SELECT Value FROM Test"; Assert.Equal(3L, await SlowQueryDbOperations.ExecuteScalarAsync(command, default));
        await using (var reader = await SlowQueryDbOperations.ExecuteReaderAsync(command, CommandBehavior.SequentialAccess, default)) {
            Assert.True(await reader.ReadAsync()); Assert.Equal(3L, reader.GetInt64(0)); Assert.False(await reader.ReadAsync());
        }
        Assert.Equal(4, scope.Snapshot("native").CommandCount);
        Assert.Equal(1, scope.Snapshot("native").RowsRead);
        Assert.Empty(profiles.GetTopProfiles().Items);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
    }

    /// <summary>审计关闭也要捕获短查询累计的慢请求，不写审计载荷或额外查询业务数据库。</summary>
    [Fact]
    public async Task SlowRequestIsCapturedWhenAuditIsDisabled() {
        var (_, profiles, pipeline) = Build(100);
        var context = new DefaultHttpContext(); context.TraceIdentifier = "aggregate-request"; context.Request.Path = "/api/parcels/analysis";
        var middleware = new WebRequestAuditLogMiddleware(async _ => {
            for (var index = 0; index < 6; index++) {
                await Task.Delay(25);
                pipeline.CommandCompleted("SELECT Value FROM parcels", TimeSpan.FromMilliseconds(25), "SqliteConnection", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }, OptionValues.Create(new WebRequestAuditLogOptions { Enabled = false }), new WebRequestAuditBuffer(4), pipeline);
        await middleware.InvokeAsync(context);
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("request", profile.Kind);
        Assert.Equal("aggregate-request", profile.TraceId);
        Assert.Equal(6, profile.LatestCommandCount);
        Assert.Equal(150m, profile.AverageExecuteMilliseconds);
        Assert.Equal(200, profile.StatusCode);
        Assert.Empty(pipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>错误响应即使在上游被处理，也要与普通慢 SQL 区分。</summary>
    [Fact]
    public async Task FailedResponseIsCapturedWithoutSql() {
        var (_, profiles, pipeline) = Build(600000);
        var context = new DefaultHttpContext(); context.Request.Path = "/api/parcels/analysis";
        var middleware = new WebRequestAuditLogMiddleware(http => { http.Response.StatusCode = 500; return Task.CompletedTask; },
            OptionValues.Create(new WebRequestAuditLogOptions { Enabled = false }), new WebRequestAuditBuffer(4), pipeline);
        await middleware.InvokeAsync(context);
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("request", profile.Kind); Assert.Equal(1, profile.ErrorCount); Assert.Equal(500, profile.StatusCode);
    }

    /// <summary>连接失败也归档独立的连接画像，而不能伪装成 SQL 执行失败。</summary>
    [Fact]
    public async Task ConnectionFailureHasItsOwnCategory() {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=" + Path.Combine(_directory, "missing", "db.sqlite") + ";Mode=ReadWrite");
        await using var db = Database(connection, pipeline, profiles);
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.OpenConnectionAsync());
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("connection", profile.Kind); Assert.Equal(1, profile.ErrorCount);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
    }

    /// <summary>结构不同的长 SQL 不得因为展示裁剪合并，注释与四提供器参数不导致指纹爆炸。</summary>
    [Fact]
    public void FingerprintsUseFullStructureAndSeparateProviders() {
        var prefix = "SELECT " + new string('x', 5000);
        Assert.NotEqual(SlowQueryFingerprintAggregator.Create(prefix + " FROM first_table").Fingerprint,
            SlowQueryFingerprintAggregator.Create(prefix + " FROM other_table").Fingerprint);
        Assert.Equal(SlowQueryFingerprintAggregator.Create("/*request abc*/ SELECT * FROM parcels WHERE Id=:name").Fingerprint,
            SlowQueryFingerprintAggregator.Create("--request xyz\nSELECT * FROM parcels WHERE Id=@other").Fingerprint);
        Assert.Equal(SlowQueryFingerprintAggregator.Create("SELECT * FROM parcels WHERE Id=:0").Fingerprint,
            SlowQueryFingerprintAggregator.Create("SELECT * FROM parcels WHERE Id=?123").Fingerprint);
        var (_, profiles, pipeline) = Build();
        pipeline.Collect("SELECT * FROM parcels", TimeSpan.FromMilliseconds(800), observation: new() { Provider = "SqliteConnection" });
        pipeline.Collect("SELECT * FROM parcels", TimeSpan.FromMilliseconds(800), observation: new() { Provider = "MySqlConnection" });
        Assert.Equal(2, profiles.GetTopProfiles().Items.Count);
    }

    /// <summary>包装后的超时、取消和 Oracle 死锁分类仍可见，错误正文不进入归档合同。</summary>
    [Fact]
    public void WrappedFailuresAndMaintenanceQueriesAreDiagnosed() {
        var (_, profiles, pipeline) = Build(600000);
        pipeline.Collect("SELECT * FROM timeout_table", TimeSpan.FromTicks(1), exception: new InvalidOperationException("外层", new TimeoutException()));
        pipeline.Collect("SELECT * FROM cancel_table", TimeSpan.FromTicks(1), exception: new AggregateException(new OperationCanceledException()));
        pipeline.Collect("SELECT * FROM deadlock_table", TimeSpan.FromTicks(1), exception: new SlowQueryNumberedException(60));
        var items = profiles.GetTopProfiles().Items;
        Assert.Contains(items, item => item.TimeoutCount == 1);
        Assert.Contains(items, item => item.CanceledCount == 1 && item.ErrorCount == 0);
        Assert.Contains(items, item => item.DeadlockCount == 1);
        var (_, maintenanceProfiles, maintenancePipeline) = Build();
        maintenancePipeline.Collect("/*AUTO_TUNING*/ SELECT * FROM metadata_table", TimeSpan.FromMilliseconds(800));
        Assert.Single(maintenanceProfiles.GetTopProfiles().Items);
        Assert.Empty(maintenancePipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>关闭采集不得被直接记录重载绕过；排序外指纹、淘汰和过期均需可见。</summary>
    [Fact]
    public void DisabledAndBoundedCollectionStatesAreHonest() {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "false"
        }).Build();
        var disabled = new SlowQueryProfileStore(config);
        disabled.Record(new("SELECT 1", "", 900m, 0, false, false, false, DateTime.Now));
        Assert.Empty(disabled.GetTopProfiles().Items); Assert.False(disabled.GetCollectionStatus().Enabled);
        var (_, profiles, _) = Build();
        for (var index = 0; index < 65; index++) profiles.Record("SELECT * FROM table" + index, TimeSpan.FromMilliseconds(600));
        Assert.Equal(65, profiles.GetTopProfiles().Items.Count);
        profiles.Record(new("SELECT * FROM expired_table", "", 900m, 0, false, false, false, DateTime.Now.AddHours(-1)));
        Assert.Equal(65, profiles.GetTopProfiles().Items.Count); Assert.True(profiles.GetCollectionStatus().ExpiredSamples > 0);
    }

    /// <summary>归档恢复原始分阶段、错误和追踪信息；先写新样本再恢复旧样本仍正确排序。</summary>
    [Fact]
    public async Task ArchiveRestoresWindowAndMetadataWithoutRequeuing() {
        var (config, profiles, pipeline) = Build();
        using var writer = new SlowQueryArchiveWorker(profiles, config);
        await writer.InitializeAsync(default);
        pipeline.Collect("SELECT * FROM parcels WHERE Barcode='private-code'", TimeSpan.FromMilliseconds(800),
            observation: new() { Provider = "SqliteConnection", TraceId = "durable-trace", ReadMilliseconds = 700m, RowsRead = 12 });
        Assert.True(profiles.ArchiveQueue.Reader.TryRead(out var pending));
        await writer.FlushAsync([pending], default); profiles.ArchiveCompleted(1, true);
        var recovered = new SlowQueryProfileStore(config);
        using var restorer = new SlowQueryArchiveWorker(recovered, config);
        await restorer.InitializeAsync(default);
        var profile = Assert.Single(recovered.GetTopProfiles().Items);
        Assert.Equal("durable-trace", profile.TraceId); Assert.Equal(700m, profile.AverageReadMilliseconds); Assert.Equal(12, profile.TotalRowsRead);
        Assert.DoesNotContain("private-code", profile.SampleSql, StringComparison.Ordinal);
        Assert.Equal(1, recovered.GetCollectionStatus().RestoredSamples);
        Assert.True(recovered.GetCollectionStatus().ArchiveReady);
        Assert.False(recovered.ArchiveQueue.Reader.TryRead(out _));
        var response = new GetSlowQueryProfileQueryService(recovered).Execute();
        Assert.Equal("durable-trace", Assert.Single(response.Items).TraceId);
        Assert.True(response.Collection.ArchiveReady);
    }

    /// <summary>后台归档队列饱和不阻塞采集，内存和丢失计数仍可查询。</summary>
    [Fact]
    public void ArchiveBackpressureIsVisibleAndNonBlocking() {
        var (_, profiles, pipeline) = Build();
        for (var index = 0; index < 4100; index++) pipeline.Collect("SELECT * FROM parcels", TimeSpan.FromMilliseconds(800));
        var status = profiles.GetCollectionStatus();
        Assert.Equal(4096, status.ArchivePending); Assert.Equal(4, status.ArchiveDropped);
        Assert.True(status.CapacityEvictions > 0); Assert.Single(profiles.GetTopProfiles().Items);
    }

    /// <summary>引号标识符、优化器提示和注释边界影响结构，不能在归一化时错误合并。</summary>
    [Fact]
    public void SqlStructureKeepsQuotedNamesAndOptimizerHints() {
        Assert.NotEqual(SlowQueryFingerprintAggregator.Create("SELECT * FROM \"ParcelA\"").Fingerprint,
            SlowQueryFingerprintAggregator.Create("SELECT * FROM \"parcela\"").Fingerprint);
        Assert.NotEqual(SlowQueryFingerprintAggregator.Create("SELECT /*+ INDEX(p IX_A) */ * FROM parcels p").Fingerprint,
            SlowQueryFingerprintAggregator.Create("SELECT /*+ INDEX(p IX_B) */ * FROM parcels p").Fingerprint);
        Assert.Equal(SlowQueryFingerprintAggregator.Create("SELECT /* tag1 */ /* tag2 */ * FROM parcels WHERE id=1").Fingerprint,
            SlowQueryFingerprintAggregator.Create("SELECT * FROM parcels WHERE id=2").Fingerprint);
        Assert.Contains("Table2026", SlowQueryFingerprintAggregator.SanitizeSql("SELECT * FROM \"Table2026\" WHERE Id=42"), StringComparison.Ordinal);
    }

    /// <summary>覆盖提供器转义和备用引号语法，不能把条码或管理员密码写入持久化诊断。</summary>
    [Theory]
    [InlineData("SELECT 'private-secret'", "SqlConnection")]
    [InlineData("SELECT 'O''private-secret'", "SqlConnection")]
    [InlineData("SELECT 'a\\'private-secret'", "MySqlConnection")]
    [InlineData("SELECT \"private-secret\"", "MySqlConnection")]
    [InlineData("SELECT q'[O'private-secret]' FROM dual", "OracleConnection")]
    [InlineData("CREATE USER app IDENTIFIED BY \"private-secret\"", "OracleConnection")]
    public void SqlLiteralVariantsAreSanitized(string sql, string provider) {
        Assert.DoesNotContain("private-secret", SlowQueryFingerprintAggregator.SanitizeSql(sql, provider), StringComparison.Ordinal);
        Assert.DoesNotContain("private-secret", SlowQueryFingerprintAggregator.NormalizeSql(sql, provider), StringComparison.Ordinal);
    }

    /// <summary>SQL Server 的 1205 是死锁，MySQL 的 1205 是锁等待超时。</summary>
    [Fact]
    public void SharedErrorNumberUsesProviderSemantics() {
        var (_, profiles, pipeline) = Build(600000);
        pipeline.Collect("SELECT * FROM mysql_wait", TimeSpan.FromTicks(1), exception: new SlowQueryNumberedException(1205), observation: new() { Provider = "MySqlConnection" });
        pipeline.Collect("SELECT * FROM sqlserver_wait", TimeSpan.FromTicks(1), exception: new SlowQueryNumberedException(1205), observation: new() { Provider = "SqlConnection" });
        Assert.Contains(profiles.GetTopProfiles().Items, item => item.Provider == "MySqlConnection" && item.TimeoutCount == 1 && item.DeadlockCount == 0);
        Assert.Contains(profiles.GetTopProfiles().Items, item => item.Provider == "SqlConnection" && item.TimeoutCount == 0 && item.DeadlockCount == 1);
    }

    /// <summary>EF 同步与异步保存点、回滚和提交实际执行，不能把事务存续时间误计为等待。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EfTransactionsAndSavepointsDoNotDoubleCountCommands(bool async) {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var db = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection)
            .AddInterceptors(new SlowQueryCommandInterceptor(pipeline, profiles), new SlowQueryConnectionInterceptor(pipeline), new SlowQueryTransactionInterceptor(pipeline)).Options);
        using var scope = SlowQueryRequestScope.Begin("transaction-test");
        await using var transaction = async ? await db.Database.BeginTransactionAsync() : db.Database.BeginTransaction();
        if (async) {
            await transaction.CreateSavepointAsync("point"); await transaction.RollbackToSavepointAsync("point");
            await transaction.ReleaseSavepointAsync("point"); await transaction.CommitAsync();
        }
        else {
            transaction.CreateSavepoint("point"); transaction.RollbackToSavepoint("point");
            transaction.ReleaseSavepoint("point"); transaction.Commit();
        }
        Assert.Equal(0, scope.Snapshot("transaction-test").CommandCount);
        Assert.True(scope.Snapshot("transaction-test").ExecuteMilliseconds > 0);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
        Assert.Empty(profiles.GetTopProfiles().Items);
        pipeline.TransactionCompleted("SqliteConnection", "forced-commit", "COMMIT", TimeSpan.FromMilliseconds(700000));
        Assert.Equal("transaction", Assert.Single(profiles.GetTopProfiles().Items).Kind);
        Assert.Empty(pipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>归档重试提交结果未知的批次，不应在重启恢复时把同一观测重复计数。</summary>
    [Fact]
    public async Task ArchiveRetryIsIdempotentAndPrunesExpiredHistory() {
        var (config, profiles, pipeline) = Build();
        using var writer = new SlowQueryArchiveWorker(profiles, config); await writer.InitializeAsync(default);
        pipeline.Collect("SELECT * FROM parcels", TimeSpan.FromMilliseconds(800), observation: new() { CommandId = "durable-once" });
        Assert.True(profiles.ArchiveQueue.Reader.TryRead(out var pending));
        var expired = new SlowQuerySample("SELECT * FROM old_table", "old", 800m, 0, false, false, false, DateTime.Now.AddDays(-10));
        await writer.FlushAsync([pending, (SlowQueryFingerprintAggregator.Create(expired.CommandText), expired)], default);
        await writer.FlushAsync([pending], default);
        await using var db = new SlowQueryArchiveDbContext(writer.ArchivePath);
        Assert.Equal(1, await db.Set<SlowQueryArchiveRow>().CountAsync());
        var recovered = new SlowQueryProfileStore(config); using var restorer = new SlowQueryArchiveWorker(recovered, config);
        await restorer.InitializeAsync(default); Assert.Equal(1, Assert.Single(recovered.GetTopProfiles().Items).CallCount);
    }

    /// <summary>后台启动后立即停止仍应写完已入队记录；不用实际站点或业务数据库。</summary>
    [Fact]
    public async Task ArchiveWorkerFlushesQueuedSamplesDuringShutdown() {
        var (config, profiles, pipeline) = Build(); using var worker = new SlowQueryArchiveWorker(profiles, config);
        await worker.StartAsync(default);
        pipeline.Collect("SELECT * FROM shutdown_table", TimeSpan.FromMilliseconds(800));
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StopAsync(shutdown.Token);
        Assert.Equal(0, profiles.GetCollectionStatus().ArchivePending);
        Assert.Equal(0, profiles.GetCollectionStatus().ArchiveDropped);
        await using var db = new SlowQueryArchiveDbContext(worker.ArchivePath);
        Assert.Equal(1, await db.Set<SlowQueryArchiveRow>().CountAsync());
    }

    /// <summary>损坏的归档记录只计入采集故障，不阻止其他有效历史样本恢复。</summary>
    [Fact]
    public async Task CorruptArchiveRowsAreSkippedWithVisibleFailures() {
        var (config, profiles, pipeline) = Build(); using var writer = new SlowQueryArchiveWorker(profiles, config);
        await writer.InitializeAsync(default); pipeline.Collect("SELECT * FROM valid_table", TimeSpan.FromMilliseconds(800));
        Assert.True(profiles.ArchiveQueue.Reader.TryRead(out var pending)); await writer.FlushAsync([pending], default);
        await using (var db = new SlowQueryArchiveDbContext(writer.ArchivePath)) {
            db.AddRange(new SlowQueryArchiveRow { Fingerprint = "invalid", OccurredAt = DateTime.Now, Payload = "{" },
                new SlowQueryArchiveRow { Fingerprint = "null-values", OccurredAt = DateTime.Now, Payload = "{\"CommandText\":\"SELECT 1\",\"Observation\":null}" });
            await db.SaveChangesAsync();
        }
        var recovered = new SlowQueryProfileStore(config); using var reader = new SlowQueryArchiveWorker(recovered, config);
        await reader.InitializeAsync(default);
        Assert.Single(recovered.GetTopProfiles().Items); Assert.True(recovered.GetCollectionStatus().ArchiveReady);
        Assert.Equal(2, recovered.GetCollectionStatus().CollectionFailures);
    }

    /// <summary>并行请求和嵌套范围的统计彼此隔离，读库累计不能污染其他请求。</summary>
    [Fact]
    public async Task ParallelAndNestedRequestScopesRemainIsolated() {
        var (_, profiles, pipeline) = Build(600000);
        /// <summary>模拟独立异步调用链中的命令完成。</summary>
        async Task<SlowQueryObservation> Request(string name, int commands) {
            using var scope = SlowQueryRequestScope.Begin(name);
            for (var index = 0; index < commands; index++) {
                await Task.Yield(); pipeline.CommandCompleted("SELECT 1", TimeSpan.FromMilliseconds(1), "SqliteConnection", name + index);
            }
            return scope.Snapshot(name);
        }
        var snapshots = await Task.WhenAll(Task.Run(() => Request("one", 3)), Task.Run(() => Request("two", 7)));
        Assert.Equal(3, snapshots[0].CommandCount); Assert.Equal(7, snapshots[1].CommandCount);
        using var parent = SlowQueryRequestScope.Begin("parent");
        using (var child = SlowQueryRequestScope.Begin("child")) {
            pipeline.CommandCompleted("SELECT 1", TimeSpan.FromMilliseconds(1), "SqliteConnection", "nested");
            Assert.Equal(1, child.Snapshot("child").CommandCount);
        }
        pipeline.CommandCompleted("SELECT 1", TimeSpan.FromMilliseconds(1), "SqliteConnection", "parent");
        Assert.Equal(1, parent.Snapshot("parent").CommandCount); Assert.Empty(profiles.GetTopProfiles().Items);
    }

    /// <summary>机器 SignalR 单次调用的慢与失败都可见；调用参数和密钥不进入观测动作。</summary>
    [Fact]
    public async Task SignalRInvocationIsDiagnosedWithoutArguments() {
        var (_, profiles, pipeline) = Build(50);
        var hub = new SlowQueryTestHub(); var method = typeof(SlowQueryTestHub).GetMethod(nameof(SlowQueryTestHub.Read))!;
        var invocation = new HubInvocationContext(null!, null!, hub, method, ["private-secret"]);
        var filter = new SlowQueryHubFilter(pipeline);
        var result = await filter.InvokeMethodAsync(invocation, async _ => {
            for (var index = 0; index < 4; index++) {
                await Task.Delay(20); pipeline.CommandCompleted("SELECT 1", TimeSpan.FromMilliseconds(20), "SqliteConnection", "hub" + index);
            }
            return "unchanged";
        });
        Assert.Equal("unchanged", result);
        var request = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("request", request.Kind); Assert.Equal(4, request.LatestCommandCount);
        Assert.Equal(32, request.TraceId.Length); Assert.DoesNotContain("private-secret", request.SampleSql, StringComparison.Ordinal);
        await Assert.ThrowsAsync<HubException>(async () => await filter.InvokeMethodAsync(invocation, _ => throw new HubException("private-secret")));
        Assert.Equal(1, Assert.Single(profiles.GetTopProfiles().Items).ErrorCount);
    }

    /// <summary>配置历史的真实 EF 读写和错误也采集，不能驱动主业务库自动创建索引。</summary>
    [Fact]
    public void AuxiliaryHistorySqlUsesSeparateRoleAndSafeSamples() {
        var (_, profiles, pipeline) = Build(600000);
        var history = new ConfigurationHistoryStore(Path.Combine(_directory, "configuration-history.db"));
        history.AttachQueryDiagnostics(pipeline);
        using var scope = SlowQueryRequestScope.Begin("history");
        history.Prepare("rules", "1", "2", ConfigurationDocument.Parse("{\"value\":\"private-secret\"}"), ConfigurationDocument.Parse("{\"value\":\"updated-secret\"}"));
        Assert.Single(history.Read()); Assert.True(scope.Snapshot("history").CommandCount >= 2);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = history.DatabasePath, Pooling = false }.ToString())) {
            connection.Open(); using var drop = connection.CreateCommand(); drop.CommandText = "DROP TABLE ConfigurationChanges"; drop.ExecuteNonQuery();
        }
        Assert.Throws<SqliteException>(() => history.Read());
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("configuration", profile.DatabaseRole); Assert.Equal(1, profile.ErrorCount);
        Assert.DoesNotContain("private-secret", profile.SampleSql, StringComparison.Ordinal); Assert.Empty(pipeline.Analyze(new MySqlDialect()).Metrics);
    }

    /// <summary>调用方已打开连接，首次原生命令仍需继承 EF 诊断，不依赖连接打开回调。</summary>
    [Fact]
    public async Task MetadataReadsOnExternallyOpenedConnectionsRemainObserved() {
        var (config, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection)
            .AddInterceptors(new SlowQueryCommandInterceptor(pipeline, profiles)).Options);
        using var scope = SlowQueryRequestScope.Begin("external-open");
        var missing = await new SqliteDialect(config).FindMissingTablesAsync(db, null, ["missing_table"], default);
        Assert.Equal(CachedMissingTableValues, missing); Assert.Equal(1, scope.Snapshot("external-open").CommandCount);
        Assert.Equal(ConnectionState.Open, connection.State); Assert.Empty(profiles.GetTopProfiles().Items);
    }

    /// <summary>既有原生备份事务的提交取消必须保留原异常，并从事务而非 SQL 类别查询。</summary>
    [Fact]
    public async Task NativeTransactionCancellationIsVisible() {
        var (_, profiles, pipeline) = Build(600000);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        SlowQueryDbOperations.Attach(connection, pipeline); await SlowQueryDbOperations.OpenAsync(connection, default);
        await using var transaction = await SlowQueryDbOperations.BeginTransactionAsync(connection, () => connection.BeginTransactionAsync());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SlowQueryDbOperations.CommitAsync(transaction, cancel.Token));
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal("transaction", profile.Kind); Assert.Equal(1, profile.CanceledCount); Assert.Equal(0, profile.ErrorCount);
        Assert.Equal(0, profiles.GetCollectionStatus().ActiveOperations);
    }

    /// <summary>审计关闭和外层尚未生成响应时，异常不能被错误标为成功的 200 请求。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UnhandledRequestFailurePreservesExceptionAndHonestStatus(bool cancel, bool wrapped) {
        var (_, profiles, pipeline) = Build(600000);
        Exception failure = cancel ? new OperationCanceledException() : new InvalidOperationException("private-secret");
        if (wrapped) failure = new InvalidOperationException("包装的执行故障", failure);
        var middleware = new WebRequestAuditLogMiddleware(_ => throw failure,
            OptionValues.Create(new WebRequestAuditLogOptions { Enabled = false }), new(10), pipeline);
        var context = new DefaultHttpContext(); context.Request.Path = "/api/parcels/private-code"; context.Request.QueryString = new("?password=private-secret");
        var caught = await Record.ExceptionAsync(() => middleware.InvokeAsync(context)); Assert.Same(failure, caught);
        var profile = Assert.Single(profiles.GetTopProfiles().Items);
        Assert.Equal(cancel ? 0 : 500, profile.StatusCode); Assert.Equal(cancel ? 0 : 1, profile.ErrorCount);
        Assert.Equal(cancel ? 1 : 0, profile.CanceledCount); Assert.DoesNotContain("private-code", profile.SampleSql, StringComparison.Ordinal);
        Assert.DoesNotContain("private-secret", profile.SampleSql, StringComparison.Ordinal);
    }

    /// <summary>为测试建立独立配置、存储和管线，保留正常归档开关。</summary>
    private (IConfiguration Configuration, SlowQueryProfileStore Profiles, SlowQueryAutoTuningPipeline Pipeline) Build(int threshold = 500) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "1",
            ["Persistence:AutoTuning:SlowQueryProfile:ArchiveDirectory"] = _directory
        }).Build();
        var profiles = new SlowQueryProfileStore(configuration);
        return (configuration, profiles, new(configuration, new NullAutoTuningObservability(), profiles));
    }
    /// <summary>通过正式 EF 拦截器建立空模型 SQLite 上下文，不操作任何业务表。</summary>
    private static DbContext Database(SqliteConnection connection, SlowQueryAutoTuningPipeline pipeline, SlowQueryProfileStore profiles) =>
        new(new DbContextOptionsBuilder().UseSqlite(connection).AddInterceptors(new SlowQueryCommandInterceptor(pipeline, profiles), new SlowQueryConnectionInterceptor(pipeline)).Options);
    /// <summary>先验证绝对路径确属本测试根目录，再清理本实例临时归档。</summary>
    public void Dispose() {
        var target = Path.GetFullPath(_directory);
        if (!target.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("测试清理路径越界。");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
