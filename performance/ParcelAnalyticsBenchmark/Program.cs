using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NLog;
using NLog.Config;
using NLog.Targets;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Performance.ParcelAnalyticsBenchmark;

/// <summary>在强制隔离的本机数据库中生成确定性来源事实并测量真实仓储与报表查询。</summary>
internal static class Program {
    /// <summary>工具异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>基准来源实例标识。</summary>
    private const string SourceInstanceId = "zeye-bench-local";

    /// <summary>执行隔离校验、预建、写入、查询和实际SQL执行计划采集。</summary>
    private static async Task<int> Main() {
        try {
            if (Environment.GetEnvironmentVariable("ZEYE_BENCH_VERBOSE_ERRORS") == "1") {
                var logging = new LoggingConfiguration();
                logging.AddRule(LogLevel.Error, LogLevel.Fatal, new ConsoleTarget("benchmark-errors") {
                    Error = true, Layout = "${level}: ${message} ${exception:format=tostring}"
                });
                LogManager.Configuration = logging;
            }
            if (Environment.GetEnvironmentVariable("ZEYE_BENCH_ISOLATED") != "1")
                throw new InvalidOperationException("必须显式设置ZEYE_BENCH_ISOLATED=1，且仅使用隔离数据库。");
            var provider = Read("ZEYE_BENCH_PROVIDER", "MySql");
            var connection = provider switch {
                "MySql" => Environment.GetEnvironmentVariable("ZEYE_BENCH_MYSQL"),
                "SqlServer" => Environment.GetEnvironmentVariable("ZEYE_BENCH_SQLSERVER"),
                _ => throw new ArgumentException("ZEYE_BENCH_PROVIDER只能为MySql或SqlServer。")
            } ?? throw new InvalidOperationException($"缺少ZEYE_BENCH_{provider.ToUpperInvariant()}连接字符串。");
            string server;
            string database;
            int? port;
            if (provider == "MySql") {
                var address = new MySqlConnectionStringBuilder(connection);
                if (address.Server is not ("127.0.0.1" or "localhost") || address.Port is <= 1024 or 3306
                    || !address.Database.StartsWith("zeye_bench_", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("压测仅允许非3306端口的本机zeye_bench_前缀隔离库。");
                server = address.Server;
                database = address.Database;
                port = (int)address.Port;
            }
            else {
                var address = new SqlConnectionStringBuilder(connection);
                var isolatedLocalDb = address.DataSource.StartsWith(@"(localdb)\ZeyeQueryBench", StringComparison.OrdinalIgnoreCase);
                if (!isolatedLocalDb || !address.InitialCatalog.StartsWith("zeye_bench_", StringComparison.OrdinalIgnoreCase)
                    || !address.IntegratedSecurity || address.AttachDBFilename.Length > 0)
                    throw new InvalidOperationException("SQL Server压测仅允许专用ZeyeQueryBench LocalDB实例中的zeye_bench_前缀数据库和集成认证。");
                server = address.DataSource;
                database = address.InitialCatalog;
                port = null;
            }

            var start = DateTime.ParseExact(Read("ZEYE_BENCH_START_DATE", "2026-08-15"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var days = ReadInt("ZEYE_BENCH_DAYS", 31, 31, 366);
            var parcelsPerDay = ReadInt("ZEYE_BENCH_PARCELS_PER_DAY", 20, 1, 10000);
            var concurrency = ReadInt("ZEYE_BENCH_CONCURRENCY", 4, 1, 32);
            var iterations = ReadInt("ZEYE_BENCH_ITERATIONS", 7, 2, 100);
            var readConcurrency = ReadInt("ZEYE_BENCH_READ_CONCURRENCY", 4, 1, 32);
            var readRequests = ReadInt("ZEYE_BENCH_READ_REQUESTS", 40, 2, 1000);
            var mixedWrites = ReadInt("ZEYE_BENCH_MIXED_WRITES", 0, 0, 10000);
            var fanoutConcurrency = ReadInt("ZEYE_BENCH_READ_FANOUT_CONCURRENCY", 4, 1, 8);
            var fanoutMaxPartitions = ReadInt("ZEYE_BENCH_READ_FANOUT_MAX_PARTITIONS", 12, 1, 32);
            var granularity = Read("ZEYE_BENCH_GRANULARITY", "PerWeek");
            var queryOnly = Environment.GetEnvironmentVariable("ZEYE_BENCH_QUERY_ONLY") == "1";
            if (mixedWrites > 0 && !queryOnly)
                throw new InvalidOperationException("混合读写压测必须对已完成造数的隔离库设置ZEYE_BENCH_QUERY_ONLY=1。");
            if (granularity is not ("PerDay" or "PerWeek" or "PerMonth"))
                throw new ArgumentException("ZEYE_BENCH_GRANULARITY只能为PerDay、PerWeek或PerMonth。");
            var runId = $"bench-{start:yyyyMMdd}-{days}-{parcelsPerDay}";
            var output = Path.GetFullPath(Read("ZEYE_BENCH_OUTPUT", ".codex-artifacts/benchmark-results/parcel-analytics-sample.json"));
            var interceptor = new AnalyticsSqlCaptureInterceptor();
            var optionsBuilder = new DbContextOptionsBuilder<SortingHubDbContext>();
            if (provider == "MySql") optionsBuilder.UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46)), mysql =>
                mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(1), null));
            else optionsBuilder.UseSqlServer(connection, sqlServer => sqlServer
                .MigrationsAssembly(SqlServerMigrationAssembly.Name)
                .EnableRetryOnFailure(5, TimeSpan.FromSeconds(1), null));
            var options = optionsBuilder.AddInterceptors(interceptor).Options;
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
                ["Persistence:Sharding:WriteRouting:DryRun"] = "false",
                ["Persistence:Sharding:ReadFanout:Enabled"] = Read("ZEYE_BENCH_READ_FANOUT", "true"),
                ["Persistence:Sharding:ReadFanout:MaxConcurrency"] = fanoutConcurrency.ToString(CultureInfo.InvariantCulture),
                ["Persistence:Sharding:ReadFanout:MaxPartitions"] = fanoutMaxPartitions.ToString(CultureInfo.InvariantCulture)
            }).Build();
            var partitions = new ParcelPartitionStore(factory, config);
            var writer = new ParcelProcessingRepository(factory, partitions);
            var reader = new ParcelAnalyticsReadService(factory, partitions,
                new ReportingQueryBudgetPlanner(Options.Create(new ReadOnlyDatabaseOptions())));
            var parcelReader = new ParcelRepository(factory, config, partitions);
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

            // 使用同一批隔离样本测量生产仓储的列表、游标与精确详情读取。
            var parcelQueries = new List<object>();
            foreach (var window in windows) {
                var from = start.AddDays(days - window);
                var filter = new ParcelQueryFilter {
                    ScannedTimeStart = from,
                    ScannedTimeEnd = start.AddDays(days).AddTicks(-1)
                };
                var expectedCount = (long)window * parcelsPerDay;
                parcelQueries.Add(await MeasureQueryAsync($"{window}d-offset-count", iterations, async () => {
                    var page = await parcelReader.GetPagedAsync(filter,
                        new PageRequest { PageSize = 50, IncludeTotalCount = true }, default);
                    if (page.TotalCount != expectedCount || page.Items.Count != Math.Min(50, expectedCount))
                        throw new InvalidOperationException($"{window}天包裹分页口径不符。");
                    return $"count={page.TotalCount};ids={string.Join(',', page.Items.Select(x => x.Id))}";
                }));
                parcelQueries.Add(await MeasureQueryAsync($"{window}d-offset-no-count", iterations, async () => {
                    var page = await parcelReader.GetPagedAsync(filter,
                        new PageRequest { PageSize = 50, IncludeTotalCount = false }, default);
                    return $"ids={string.Join(',', page.Items.Select(x => x.Id))}";
                }));
                parcelQueries.Add(await MeasureQueryAsync($"{window}d-cursor-first", iterations, async () => {
                    var page = await parcelReader.GetCursorPagedAsync(filter,
                        new CursorPageRequest { PageSize = 50 }, default);
                    if (page.Items.Count != Math.Min(50, expectedCount) || page.HasMore != (expectedCount > 50))
                        throw new InvalidOperationException($"{window}天包裹游标口径不符。");
                    return $"more={page.HasMore};ids={string.Join(',', page.Items.Select(x => x.Id))}";
                }));
            }
            var fullRange = new ParcelQueryFilter {
                ScannedTimeStart = start.AddDays(days - 31),
                ScannedTimeEnd = start.AddDays(days).AddTicks(-1)
            };
            var firstCursorPage = await parcelReader.GetCursorPagedAsync(fullRange,
                new CursorPageRequest { PageSize = 50 }, default);
            if (firstCursorPage.HasMore)
                parcelQueries.Add(await MeasureQueryAsync("31d-cursor-second", iterations, async () => {
                    var page = await parcelReader.GetCursorPagedAsync(fullRange,
                        new CursorPageRequest { PageSize = 50, LastScannedTimeLocal = firstCursorPage.NextScannedTimeLocal,
                            LastId = firstCursorPage.NextId }, default);
                    if (page.Items.Count != Math.Min(50, (long)31 * parcelsPerDay - firstCursorPage.Items.Count)
                        || page.Items.Any(x => firstCursorPage.Items.Any(y => x.Id == y.Id)))
                        throw new InvalidOperationException("包裹游标续页口径不符。");
                    return $"more={page.HasMore};ids={string.Join(',', page.Items.Select(x => x.Id))}";
                }));
            if ((long)31 * parcelsPerDay >= 5500)
                parcelQueries.Add(await MeasureQueryAsync("31d-offset-deep", iterations, async () => {
                    var page = await parcelReader.GetPagedAsync(fullRange,
                        new PageRequest { PageNumber = 110, PageSize = 50, IncludeTotalCount = true }, default);
                    if (page.TotalCount != (long)31 * parcelsPerDay || page.Items.Count != 50)
                        throw new InvalidOperationException("包裹深页口径不符。");
                    return $"count={page.TotalCount};ids={string.Join(',', page.Items.Select(x => x.Id))}";
                }));
            var lastSourceId = (long)days * parcelsPerDay;
            var allFilter = fullRange with {
                BarCodeKeyword = "BENCH-" + (lastSourceId % 13 == 0 ? lastSourceId - 1 : lastSourceId)
            };
            parcelQueries.Add(await MeasureQueryAsync("31d-barcode-substring", iterations, async () => {
                var page = await parcelReader.GetCursorPagedAsync(allFilter,
                    new CursorPageRequest { PageSize = 50 }, default);
                if (page.Items.Count == 0) throw new InvalidOperationException("条码子串查询没有命中样本。");
                return $"more={page.HasMore};ids={string.Join(',', page.Items.Select(x => x.Id))}";
            }));
            var detailPage = await parcelReader.GetCursorPagedAsync(allFilter,
                new CursorPageRequest { PageSize = 1 }, default);
            var detailId = detailPage.Items[0].Id;
            parcelQueries.Add(await MeasureQueryAsync("detail-by-id", iterations, async () => {
                var parcel = await parcelReader.GetByIdAsync(detailId, default);
                if (parcel?.Id != detailId) throw new InvalidOperationException("包裹详情精确查询口径不符。");
                return $"id={parcel.Id};records={parcel.ProcessingRecords.Count}";
            }));

            var expectedCursorIds = firstCursorPage.Items.Select(item => item.Id).ToArray();
            var parallelLatencies = new ConcurrentBag<decimal>();
            var parallelClock = Stopwatch.StartNew();
            await Parallel.ForEachAsync(Enumerable.Range(0, readRequests),
                new ParallelOptions { MaxDegreeOfParallelism = readConcurrency }, async (_, token) => {
                    var timer = Stopwatch.StartNew();
                    var page = await parcelReader.GetCursorPagedAsync(fullRange,
                        new CursorPageRequest { PageSize = 50 }, token);
                    timer.Stop();
                    if (!page.Items.Select(item => item.Id).SequenceEqual(expectedCursorIds))
                        throw new InvalidOperationException("并发游标查询的结果与串行预热结果不一致。");
                    parallelLatencies.Add(ElapsedMilliseconds(timer));
                });
            parallelClock.Stop();
            var parallelReads = new {
                Concurrency = readConcurrency, Requests = readRequests,
                ElapsedSeconds = ElapsedMilliseconds(parallelClock) / 1000m,
                RequestsPerSecond = readRequests * 1000m / ElapsedMilliseconds(parallelClock),
                LatencyMs = Stats(parallelLatencies.ToArray())
            };

            var mixedReadWrite = mixedWrites > 0
                ? await MeasureMixedReadWriteAsync(writer, parcelReader, partitions, fullRange, expectedCursorIds,
                    start.AddDays(days), mixedWrites, concurrency, readConcurrency, readRequests)
                : null;

            var resultDocument = new {
                GeneratedAtLocal = DateTime.Now,
                Environment = new { Server = server, Port = port, Database = database, Provider = provider, Granularity = granularity,
                    RuntimeVersion = System.Environment.Version.ToString(), ProcessorCount = System.Environment.ProcessorCount,
                    ServerGc = System.Runtime.GCSettings.IsServerGC },
                Input = new { StartDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Days = days, ParcelsPerDay = parcelsPerDay, Concurrency = concurrency, Iterations = iterations, ReadConcurrency = readConcurrency, ReadRequests = readRequests, MixedWrites = mixedWrites, FanoutConcurrency = fanoutConcurrency, FanoutMaxPartitions = fanoutMaxPartitions, QueryOnly = queryOnly },
                Sample = new { ParcelCount = days * parcelsPerDay, FactCount = records.Count, PhysicalPeriods = periods.Length, DuplicateCount = duplicateCount },
                PrebuildMilliseconds = ElapsedMilliseconds(prebuildClock),
                Writes = new { Count = writeSamples.Count, ElapsedSeconds = ElapsedMilliseconds(writeClock) / 1000m,
                    WritesPerSecond = writeSamples.Count == 0 ? 0m : writeSamples.Count * 1000m / ElapsedMilliseconds(writeClock),
                    LatencyMs = writeSamples.Count == 0 ? (BenchmarkLatency?)null : Stats(writeSamples.ToArray()) },
                Reports = queryResults,
                ParcelQueries = parcelQueries,
                ParallelCursorReads = parallelReads,
                MixedReadWrite = mixedReadWrite,
                ExplainAnalyze = plans
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(resultDocument, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"BENCH_OK database={database} parcels={days * parcelsPerDay} facts={records.Count} periods={periods.Length} writes={writeSamples.Count} result={output}");
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

    /// <summary>预热后测量只读场景，并保存稳定结果签名以核对改动前后数据一致性。</summary>
    private static async Task<object> MeasureQueryAsync(string name, int iterations, Func<Task<string>> query) {
        var signature = await query();
        var samples = new decimal[iterations];
        for (var index = 0; index < samples.Length; index++) {
            var timer = Stopwatch.StartNew();
            var current = await query();
            timer.Stop();
            if (current != signature) throw new InvalidOperationException($"查询{name}在稳定样本上的结果发生变化。");
            samples[index] = ElapsedMilliseconds(timer);
        }
        return new { Name = name, SamplesMs = samples, LatencyMs = Stats(samples), Signature = signature };
    }

    /// <summary>在稳定历史窗口上并发读取，同时向新的时间周期追加真实来源事实。</summary>
    private static async Task<object> MeasureMixedReadWriteAsync(ParcelProcessingRepository writer,
        ParcelRepository parcelReader, ParcelPartitionStore partitions, ParcelQueryFilter filter,
        IReadOnlyList<long> expectedCursorIds, DateTime nextDay, int mixedWrites, int writeConcurrency,
        int readConcurrency, int readRequests) {
        await partitions.EnsureCreatedAsync(partitions.Resolve(nextDay), default);
        var runId = "bench-mixed-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var records = BuildRecords(nextDay, 1, mixedWrites, runId);
        var writeLatencies = new ConcurrentBag<decimal>();
        var readLatencies = new ConcurrentBag<decimal>();
        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = Stopwatch.StartNew();
        var writeTask = Task.Run(async () => {
            await startGate.Task;
            foreach (var group in records.GroupBy(record => record.Stage == ParcelProcessingStage.Detected ? 0 : 1).OrderBy(group => group.Key))
                await Parallel.ForEachAsync(group, new ParallelOptions { MaxDegreeOfParallelism = writeConcurrency }, async (record, token) => {
                    var sample = Stopwatch.StartNew();
                    var result = await writer.AppendAsync(record, token);
                    sample.Stop();
                    if (!result.IsSuccess || result.Value!.IsDuplicate)
                        throw new InvalidOperationException($"混合负载来源事实写入失败或重复：{record.RecordId} {result.ErrorCode}。");
                    writeLatencies.Add(ElapsedMilliseconds(sample));
                });
        });
        var readTask = Task.Run(async () => {
            await startGate.Task;
            await Parallel.ForEachAsync(Enumerable.Range(0, readRequests),
                new ParallelOptions { MaxDegreeOfParallelism = readConcurrency }, async (_, token) => {
                    var sample = Stopwatch.StartNew();
                    var page = await parcelReader.GetCursorPagedAsync(filter, new CursorPageRequest { PageSize = 50 }, token);
                    sample.Stop();
                    if (!page.Items.Select(item => item.Id).SequenceEqual(expectedCursorIds))
                        throw new InvalidOperationException("混合读写期间历史窗口游标结果发生变化。");
                    readLatencies.Add(ElapsedMilliseconds(sample));
                });
        });
        startGate.SetResult();
        await Task.WhenAll(writeTask, readTask);
        timer.Stop();
        return new { RunId = runId, Writes = writeLatencies.Count, Reads = readLatencies.Count,
            ElapsedSeconds = ElapsedMilliseconds(timer) / 1000m,
            WriteLatencyMs = Stats(writeLatencies.ToArray()), ReadLatencyMs = Stats(readLatencies.ToArray()) };
    }

    /// <summary>使用Stopwatch计数器换算定点毫秒值。</summary>
    private static decimal ElapsedMilliseconds(Stopwatch stopwatch) => stopwatch.ElapsedTicks * 1000m / Stopwatch.Frequency;

    /// <summary>对已执行的参数化报表SQL采集数据库实际执行计划。</summary>
    private static async Task<string> ExplainAsync(SortingHubDbContext db, string sql,
        (string Name, object? Value, DbType Type)[] parameters) {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        if (db.Database.IsSqlServer()) {
            await using var toggle = connection.CreateCommand();
            toggle.CommandText = "SET STATISTICS XML ON";
            await toggle.ExecuteNonQueryAsync();
            try { return await ExecuteSqlServerPlanAsync(connection, sql, parameters); }
            finally {
                toggle.CommandText = "SET STATISTICS XML OFF";
                await toggle.ExecuteNonQueryAsync();
            }
        }
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

    /// <summary>SQL Server在执行查询后返回独立的Showplan XML结果集。</summary>
    private static async Task<string> ExecuteSqlServerPlanAsync(System.Data.Common.DbConnection connection, string sql,
        (string Name, object? Value, DbType Type)[] parameters) {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        foreach (var item in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Name;
            parameter.Value = item.Value ?? DBNull.Value;
            parameter.DbType = item.Type;
            command.Parameters.Add(parameter);
        }
        await using var reader = await command.ExecuteReaderAsync();
        string? plan = null;
        do {
            if (reader.FieldCount == 1 && reader.GetName(0).Contains("Showplan", StringComparison.OrdinalIgnoreCase)) {
                if (await reader.ReadAsync()) plan = reader.GetValue(0).ToString();
            }
            else while (await reader.ReadAsync()) { }
        } while (await reader.NextResultAsync());
        return plan ?? throw new InvalidOperationException("SQL Server未返回实际执行计划。");
    }
}
