using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NLog;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Performance.ParcelAnalyticsBenchmark;

/// <summary>在强制隔离的MySQL库中生成确定性来源事实并测量真实仓储与报表查询。</summary>
internal static class Program {
    /// <summary>工具异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>基准来源实例标识。</summary>
    private const string SourceInstanceId = "zeye-bench-local";

    /// <summary>执行隔离校验、预建、写入、查询和实际SQL执行计划采集。</summary>
    private static async Task<int> Main() {
        try {
            if (Environment.GetEnvironmentVariable("ZEYE_BENCH_ISOLATED") != "1")
                throw new InvalidOperationException("必须显式设置ZEYE_BENCH_ISOLATED=1，且仅使用隔离MySQL实例。");
            var connection = Environment.GetEnvironmentVariable("ZEYE_BENCH_MYSQL")
                ?? throw new InvalidOperationException("缺少ZEYE_BENCH_MYSQL连接字符串。");
            var address = new MySqlConnectionStringBuilder(connection);
            if (address.Server is not ("127.0.0.1" or "localhost") || address.Port is <= 1024 or 3306
                || !address.Database.StartsWith("zeye_bench_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("压测仅允许非3306端口的本机zeye_bench_前缀隔离库。");

            var start = DateTime.ParseExact(Read("ZEYE_BENCH_START_DATE", "2026-08-15"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var days = ReadInt("ZEYE_BENCH_DAYS", 31, 31, 366);
            var parcelsPerDay = ReadInt("ZEYE_BENCH_PARCELS_PER_DAY", 20, 1, 10000);
            var concurrency = ReadInt("ZEYE_BENCH_CONCURRENCY", 4, 1, 32);
            var iterations = ReadInt("ZEYE_BENCH_ITERATIONS", 7, 2, 100);
            var granularity = Read("ZEYE_BENCH_GRANULARITY", "PerWeek");
            var queryOnly = Environment.GetEnvironmentVariable("ZEYE_BENCH_QUERY_ONLY") == "1";
            if (granularity is not ("PerDay" or "PerWeek" or "PerMonth"))
                throw new ArgumentException("ZEYE_BENCH_GRANULARITY只能为PerDay、PerWeek或PerMonth。");
            var runId = $"bench-{start:yyyyMMdd}-{days}-{parcelsPerDay}";
            var output = Path.GetFullPath(Read("ZEYE_BENCH_OUTPUT", ".codex-artifacts/benchmark-results/parcel-analytics-sample.json"));
            var interceptor = new AnalyticsSqlCaptureInterceptor();
            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46)), mysql =>
                    mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(1), null))
                .AddInterceptors(interceptor).Options;
            IDbContextFactory<SortingHubDbContext> factory = new PooledDbContextFactory<SortingHubDbContext>(options);
            await using (var db = await factory.CreateDbContextAsync()) {
                var pending = await db.Database.GetPendingMigrationsAsync();
                if (pending.Any()) throw new InvalidOperationException("隔离库尚未完成迁移，请先预览并执行现有EF迁移：" + string.Join(",", pending));
                if (!queryOnly && await db.Set<ParcelProcessingReceipt>().AnyAsync()
                    && Environment.GetEnvironmentVariable("ZEYE_BENCH_ALLOW_REPLAY") != "1")
                    throw new InvalidOperationException("隔离库已存在处理事实。仅在确认是同一参数的重复运行时设置ZEYE_BENCH_ALLOW_REPLAY=1。");
            }

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Time:Granularity"] = granularity,
                ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = "true",
                ["Persistence:Sharding:WriteRouting:DryRun"] = "false"
            }).Build();
            var partitions = new ParcelPartitionStore(factory, config);
            var writer = new ParcelProcessingRepository(factory, partitions);
            var reader = new ParcelAnalyticsReadService(factory, partitions,
                new ReportingQueryBudgetPlanner(Options.Create(new ReadOnlyDatabaseOptions())));
            var records = BuildRecords(start, days, parcelsPerDay, runId);

            // 步骤1：受现有分表DDL隔离器保护地预建周期，避免把建表耗时混入稳态写入。
            var prebuildClock = Stopwatch.StartNew();
            var periods = Enumerable.Range(0, days).Select(day => partitions.Resolve(start.AddDays(day)))
                .DistinctBy(period => period.Suffix).ToArray();
            if (!queryOnly) foreach (var period in periods) await partitions.EnsureCreatedAsync(period, default);
            prebuildClock.Stop();

            // 步骤2：通过生产仓储写入真实事实、全局凭据、定位索引和快照，并统计每次调用耗时。
            var writeSamples = new ConcurrentBag<decimal>();
            var duplicateCount = 0;
            var writeClock = Stopwatch.StartNew();
            if (!queryOnly) {
                foreach (var group in records.GroupBy(record => record.Stage == ParcelProcessingStage.Detected ? 0 : 1).OrderBy(group => group.Key)) {
                    await Parallel.ForEachAsync(group, new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async (record, token) => {
                        var timer = Stopwatch.StartNew();
                        var result = await writer.AppendAsync(record, token);
                        timer.Stop();
                        if (!result.IsSuccess) throw new InvalidOperationException($"来源事实写入失败：{record.RecordId} {result.ErrorCode} {result.ErrorMessage}");
                        if (result.Value!.IsDuplicate) Interlocked.Increment(ref duplicateCount);
                        writeSamples.Add(ElapsedMilliseconds(timer));
                    });
                }
            }
            writeClock.Stop();

            // 步骤3：分别测量1、7、31天窗口，验证包裹总体与按发生日统计的事实总体。
            var windows = new[] { 1, 7, 31 };
            var queryResults = new List<object>();
            var plans = new List<object>();
            foreach (var window in windows) {
                var from = start.AddDays(days - window);
                var to = start.AddDays(days - 1);
                await reader.GetAsync(from, to, default);
                var samples = new decimal[iterations];
                for (var i = 0; i < iterations; i++) {
                    var timer = Stopwatch.StartNew();
                    var report = await reader.GetAsync(from, to, default);
                    timer.Stop();
                    samples[i] = ElapsedMilliseconds(timer);
                    CheckReport(report.DetectedCount, report.ProcessingEventCount, report.FailedAttemptCount,
                        report.UnboundDwsEventCount, records, from, to, parcelsPerDay, window);
                }
                queryResults.Add(new { Days = window, SamplesMs = samples, LatencyMs = Stats(samples) });
                // 步骤4：按每个窗口捕获真实SQL，区分窄窗口与全月查询的扫描成本。
                interceptor.Begin();
                try { await reader.GetAsync(from, to, default); }
                finally { interceptor.End(); }
                await using var db = await factory.CreateDbContextAsync();
                var sqlQueries = interceptor.Queries.Where(query => query.Sql.Contains("Parcel_ProcessingRecords", StringComparison.Ordinal)
                    || query.Sql.Contains("Parcels", StringComparison.Ordinal)).ToArray();
                foreach (var query in sqlQueries) {
                    var plan = await ExplainAsync(db, query.Sql, query.Parameters);
                    plans.Add(new { Days = window, Sql = query.Sql, Plan = plan });
                }
            }

            var resultDocument = new {
                GeneratedAtLocal = DateTime.Now,
                Environment = new { address.Server, address.Port, address.Database, Provider = "MySQL 8.0.46", Granularity = granularity,
                    RuntimeVersion = System.Environment.Version.ToString(), ProcessorCount = System.Environment.ProcessorCount,
                    ServerGc = System.Runtime.GCSettings.IsServerGC },
                Input = new { StartDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Days = days, ParcelsPerDay = parcelsPerDay, Concurrency = concurrency, Iterations = iterations, QueryOnly = queryOnly },
                Sample = new { ParcelCount = days * parcelsPerDay, FactCount = records.Count, PhysicalPeriods = periods.Length, DuplicateCount = duplicateCount },
                PrebuildMilliseconds = ElapsedMilliseconds(prebuildClock),
                Writes = new { Count = writeSamples.Count, ElapsedSeconds = ElapsedMilliseconds(writeClock) / 1000m,
                    WritesPerSecond = writeSamples.Count == 0 ? 0m : writeSamples.Count * 1000m / ElapsedMilliseconds(writeClock),
                    LatencyMs = writeSamples.Count == 0 ? (BenchmarkLatency?)null : Stats(writeSamples.ToArray()) },
                Reports = queryResults,
                ExplainAnalyze = plans
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(resultDocument, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"BENCH_OK database={address.Database} parcels={days * parcelsPerDay} facts={records.Count} periods={periods.Length} writes={writeSamples.Count} result={output}");
            return 0;
        }
        catch (Exception exception) {
            Logger.Error(exception, "隔离包裹报表压测失败");
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally { LogManager.Shutdown(); }
    }

    /// <summary>从环境变量读取可选文本参数。</summary>
    private static string Read(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    /// <summary>读取带范围的整数参数，拒绝无界压测输入。</summary>
    private static int ReadInt(string name, int fallback, int min, int max) {
        var text = Read(name, fallback.ToString(CultureInfo.InvariantCulture));
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new ArgumentException($"{name}必须为{min}至{max}之间的整数。");
        return value;
    }

    /// <summary>构造跨周期、失败尝试、未绑定DWS和迟到事实的稳定样本。</summary>
    private static List<ParcelProcessingRecord> BuildRecords(DateTime start, int days, int perDay, string runId) {
        var records = new List<ParcelProcessingRecord>(days * (perDay + perDay / 10 + 1) + 1);
        for (var day = 0; day < days; day++) {
            var date = start.AddDays(day);
            for (var index = 0; index < perDay; index++) {
                var sourceId = (long)day * perDay + index + 1;
                var at = date.AddHours(10).AddMilliseconds(index);
                var id = $"detect-{sourceId}";
                records.Add(Fact(id, runId, sourceId, at) with {
                    Barcode = sourceId % 13 == 0 ? "NoRead" : $"BENCH-{sourceId}", WorkstationName = $"W-{sourceId % 8}"
                });
                if (sourceId % 10 == 0) records.Add(Fact($"failed-{sourceId}", runId, sourceId, at.AddSeconds(2)) with {
                    Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false, ErrorMessage = "isolated-benchmark-failure"
                });
            }
            records.Add(Fact($"unbound-{day}", runId, null, date.AddHours(11)) with {
                Stage = ParcelProcessingStage.DwsReceived, RawPayload = "isolated-benchmark-unbound"
            });
        }
        records.Add(Fact("late-first-parcel", runId, 1, start.AddDays(days - 1).AddHours(12)) with {
            Stage = ParcelProcessingStage.ScanUploaded, IsSuccess = false, RecordedAt = start.AddDays(days - 1).AddHours(13)
        });
        return records;
    }

    /// <summary>构造一个稳定的本地时间来源事实。</summary>
    private static ParcelProcessingRecord Fact(string id, string runId, long? sourceId, DateTime occurredAt) => new() {
        RecordId = id, SourceInstanceId = SourceInstanceId, SourceRunId = runId, SourceParcelId = sourceId,
        Stage = ParcelProcessingStage.Detected, OccurredAt = occurredAt, RecordedAt = occurredAt,
        PartitionTime = occurredAt, PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))
    };

    /// <summary>核对报表没有遗漏迟到或未绑定事实，也没有把事实数充当包裹数。</summary>
    private static void CheckReport(long detected, long events, long failed, long unbound,
        IReadOnlyList<ParcelProcessingRecord> records, DateTime from, DateTime to, int perDay, int window) {
        var end = to.AddDays(1);
        var periodRecords = records.Where(record => record.OccurredAt >= from && record.OccurredAt < end).ToArray();
        if (detected != (long)window * perDay || events != periodRecords.LongLength
            || failed != periodRecords.LongCount(record => record.IsSuccess == false)
            || unbound != periodRecords.LongCount(record => record.SourceParcelId is null))
            throw new InvalidOperationException($"{window}天报表口径不符：detected={detected}, events={events}, failed={failed}, unbound={unbound}。");
    }

    /// <summary>返回按最近秩定义的P50、P95和P99毫秒值。</summary>
    private static BenchmarkLatency Stats(decimal[] samples) {
        Array.Sort(samples);
        return new BenchmarkLatency(samples[(samples.Length * 50 + 99) / 100 - 1],
            samples[(samples.Length * 95 + 99) / 100 - 1],
            samples[(samples.Length * 99 + 99) / 100 - 1]);
    }

    /// <summary>使用Stopwatch计数器换算定点毫秒值。</summary>
    private static decimal ElapsedMilliseconds(Stopwatch stopwatch) => stopwatch.ElapsedTicks * 1000m / Stopwatch.Frequency;

    /// <summary>对已执行的参数化报表SQL采集MySQL实际执行计划。</summary>
    private static async Task<string> ExplainAsync(SortingHubDbContext db, string sql,
        (string Name, object? Value, DbType Type)[] parameters) {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN ANALYZE " + sql;
        command.CommandTimeout = 120;
        foreach (var item in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Name;
            parameter.Value = item.Value ?? DBNull.Value;
            parameter.DbType = item.Type;
            command.Parameters.Add(parameter);
        }
        await using var result = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await result.ReadAsync()) lines.Add(result.GetString(0));
        return string.Join(Environment.NewLine, lines);
    }
}
