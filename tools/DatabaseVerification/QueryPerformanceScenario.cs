using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>四库共用的真实 EF 读写负载；入口完成专属数据库校验后才允许执行。</summary>
internal static class QueryPerformanceScenario {
    /// <summary>固定验收来源，保证重复运行不重复造数。</summary>
    private const string Source = "query-perf-20261008";
    /// <summary>验收包裹号与协议用例隔离。</summary>
    private const long BaseId = 8_000_000_000_000_000_000L;
    /// <summary>三十日本地窗口跨物理分表。</summary>
    private static readonly DateTime Start = new(2026, 9, 9);

    /// <summary>生成关联完整的包裹和宽事实，补齐投影，再绕过缓存逐项核对统计。</summary>
    internal static async Task<object> ExecuteAsync(IServiceProvider services, int count) {
        if (count is < 100 or > 100000) throw new ArgumentException("性能验收票数必须介于100至100000。");
        var factory = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>();
        var partitions = services.GetRequiredService<ParcelPartitionStore>();
        await SeedAsync(factory, partitions, count);
        var backfill = services.GetRequiredService<ParcelDurationBackfillService>();
        var calls = services.GetRequiredService<ParcelDurationCallProjectionService>();
        var dwsBackfill = services.GetRequiredService<Zeye.Sorting.Hub.Application.Abstractions.Persistence.IParcelDwsMeasurementBackfillService>();
        while (await backfill.RunBatchAsync(default) > 0) { }
        while (await dwsBackfill.RunBatchAsync(default) > 0) { }
        while (await calls.RunBatchAsync(default) > 0) { }
        var analysis = services.GetRequiredService<IParcelAnalysisReadService>();
        var dws = services.GetRequiredService<IParcelDwsConsistencyReadService>();
        var timing = services.GetRequiredService<IParcelTimingReadService>();
        var analytics = services.GetRequiredService<IParcelAnalyticsReadService>();
        var rows = new List<object>();
        foreach (var view in new[] { "exceptions", "chutes", "duration" }) {
            var types = view == "duration" ? new[] { "completion", "dws", "routing", "sorting", "scan-upload", "chute-request", "landing-report", "image-upload", "other-api" } : new[] { "completion" };
            foreach (var type in types) {
                rows.Add(await MeasureAsync(view + ":" + type, async () => {
                    var result = await analysis.ReadAsync(new() { View = view, DurationType = type, FromDate = Start,
                        ToDate = Start.AddDays(29), SourceInstanceId = Source, RefreshDurationSnapshot = true }, default);
                    if (result.ParcelCount != count) throw new InvalidOperationException("性能验收包裹总体不完整。");
                    if (view == "duration" && result.DurationAnalysis is { } duration) {
                        var expected = type is "image-upload" or "other-api" ? 0 : count;
                        if (duration.SampleCount != expected) throw new InvalidOperationException(type + "有效样本与输入事实不符。");
                        return new { count = result.ParcelCount, samples = duration.SampleCount, duration.AverageMilliseconds, duration.MedianMilliseconds, duration.P95Milliseconds };
                    }
                    if (view == "duration" && result.LifecycleSampleCount != count) throw new InvalidOperationException("完成耗时样本丢失。");
                    return new { count = result.ParcelCount, result.CompletedCount, result.LifecycleSampleCount, result.AverageMilliseconds, result.MedianMilliseconds, result.P95Milliseconds };
                }));
            }
        }
        rows.Add(await MeasureAsync("dws-consistency", async () => {
            var result = await dws.ReadAsync(new() { FromDate = Start, ToDate = Start.AddDays(29), SourceInstanceId = Source, Refresh = true }, default);
            if (result.MeasurementCount != count) throw new InvalidOperationException("DWS 去重样本与输入事实不符。");
            return new { result.MeasurementCount, result.DuplicateRecordCount, result.MissingIdentityCount, result.ConflictingMeasurementCount };
        }));
        rows.Add(await MeasureAsync("analytics", async () => {
            var result = await analytics.GetAsync(Start, Start.AddDays(29), default);
            if (result.DetectedCount < count) throw new InvalidOperationException("报表未覆盖验收总体。");
            return new { result.DetectedCount, result.CompletedCount, result.ProcessingEventCount };
        }));
        rows.Add(await MeasureAsync("timing", async () => {
            var result = await timing.ReadAsync(BaseId + count / 2, default) ?? throw new InvalidOperationException("时序锚点丢失。");
            if (result.Items.Count != 11) throw new InvalidOperationException("邻近窗口票数不符。");
            return new { count = result.Items.Count, result.AnchorId, result.BeforeCount, result.AfterCount };
        }));
        rows.Add(await MeasureAsync("comparison", async () => {
            var result = await timing.CompareAsync([BaseId + 1, BaseId + count / 2, BaseId + count], default);
            if (result.Items.Count != 3) throw new InvalidOperationException("对比包裹丢失。");
            return new { count = result.Items.Count };
        }));
        rows.Add(await MeasureAsync("barcode-search", async () => {
            var result = await timing.SearchAsync("PERF-0050", "barcode", 1, default);
            if (result.TotalCount != Enumerable.Range(1, count).LongCount(index => index % 1000 == 50)) throw new InvalidOperationException("重复条码候选统计不符。");
            return new { result.TotalCount, count = result.Items.Count };
        }));
        var repository = services.GetRequiredService<IParcelRepository>();
        foreach (var pageNumber in new[] { 1, Math.Max(2, count / 20) }) {
            rows.Add(await MeasureAsync("parcel-page-" + pageNumber, async () => {
                var result = await repository.GetPagedAsync(new() { SourceInstanceId = Source, ScannedTimeStart = Start, ScannedTimeEnd = Start.AddDays(30) },
                    new() { PageNumber = pageNumber, PageSize = 20 }, default);
                if (result.TotalCount != count) throw new InvalidOperationException("包裹列表统计丢失。");
                return new { result.TotalCount, result.PageNumber, count = result.Items.Count };
            }));
        }
        rows.Add(await MeasureAsync("parcel-detail", async () => {
            var result = await repository.GetByIdAsync(BaseId + 1, default) ?? throw new InvalidOperationException("包裹详情丢失。");
            if (result.ProcessingRecords.Count != 9) throw new InvalidOperationException("包裹处理事实丢失。");
            return new { result.Id, facts = result.ProcessingRecords.Count };
        }));
        rows.Add(await MeasureAsync("parcel-neighbours", async () => {
            var result = await repository.GetAdjacentByIdAsync(BaseId + count / 2, 5, 5, default);
            if (!result.IsSuccess || result.Value?.Count != 10) throw new InvalidOperationException("相邻包裹窗口丢失。");
            return new { count = result.Value.Count };
        }));
        rows.Add(await MeasureAsync("parcel-cursor", async () => {
            var result = await repository.GetCursorPagedAsync(new() { SourceInstanceId = Source, ScannedTimeStart = Start, ScannedTimeEnd = Start.AddDays(30) },
                new() { PageSize = 20, LastScannedTimeLocal = CreatedAt(count / 2, count), LastId = BaseId + count / 2 }, default);
            if (result.Items.Count != 20 || !result.HasMore) throw new InvalidOperationException("游标包裹分页丢失。");
            return new { count = result.Items.Count, result.HasMore };
        }));
        var audit = services.GetRequiredService<IWebRequestAuditLogQueryRepository>();
        rows.Add(await MeasureAsync("audit-page", async () => {
            var result = await audit.GetPagedAsync(new() { RequestPathKeyword = "/performance-fixture/" }, new() { PageSize = 20 }, default);
            if (result.TotalCount != count) throw new InvalidOperationException("审计列表统计丢失。");
            return new { result.TotalCount, count = result.Items.Count };
        }));
        await using var db = await factory.CreateDbContextAsync();
        return new { provider = db.Database.ProviderName, count, from = Start, to = Start.AddDays(29), queries = rows };
    }

    /// <summary>首次调用与两次稳态调用分开记录，每次均读取真实持久化数据。</summary>
    private static async Task<object> MeasureAsync(string name, Func<Task<object>> read) {
        var elapsed = new decimal[3]; object? result = null;
        for (var index = 0; index < elapsed.Length; index++) {
            var started = Stopwatch.GetTimestamp();
            result = await read();
            elapsed[index] = Stopwatch.GetElapsedTime(started).Ticks / (decimal)TimeSpan.TicksPerMillisecond;
        }
        Console.Error.WriteLine(name + ": " + string.Join(" / ", elapsed.Select(value => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))) + " ms");
        return new { name, firstMilliseconds = elapsed[0], steadyMilliseconds = elapsed.Skip(1).ToArray(), result };
    }

    /// <summary>只新增不存在的验收编号，每批最多100票；模型建表由正式分表维护完成。</summary>
    private static async Task SeedAsync(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions, int count) {
        var groups = Enumerable.Range(1, count).GroupBy(index => partitions.Resolve(CreatedAt(index, count)).Suffix);
        foreach (var group in groups) {
            await partitions.EnsureCreatedAsync(partitions.Resolve(CreatedAt(group.First(), count)), default);
            foreach (var batch in group.Chunk(100)) {
                await using var db = await partitions.CreateContextAsync(group.Key, default);
                var ids = batch.Select(index => BaseId + index).ToArray();
                var existing = (await db.Set<Parcel>().Where(parcel => ids.Contains(parcel.Id)).Select(parcel => parcel.Id).ToArrayAsync()).ToHashSet();
                foreach (var index in batch.Where(index => !existing.Contains(BaseId + index))) {
                    var at = CreatedAt(index, count);
                    var records = Records(index, at);
                    var parcel = Parcel.CreateDetected(BaseId + index, records[0], at);
                    parcel.ApplyProcessingRecords(records);
                    db.Add(parcel); db.AddRange(records);
                }
                await db.SaveChangesAsync();
            }
        }
        foreach (var batch in Enumerable.Range(1, count).Chunk(500)) {
            await using var db = await factory.CreateDbContextAsync();
            var ids = batch.Select(index => BaseId + index).ToArray();
            var locations = (await db.Set<ParcelLocation>().Where(row => ids.Contains(row.Id)).Select(row => row.Id).ToArrayAsync()).ToHashSet();
            var traceIds = batch.Select(index => "perf-" + index).ToArray();
            var audits = (await db.Set<WebRequestAuditLog>().Where(row => traceIds.Contains(row.TraceId)).Select(row => row.TraceId).ToArrayAsync()).ToHashSet(StringComparer.Ordinal);
            foreach (var index in batch) {
                var at = CreatedAt(index, count);
                if (!locations.Contains(BaseId + index)) db.Add(new ParcelLocation { Id = BaseId + index, CreatedTime = at, Suffix = partitions.Resolve(at).Suffix });
                if (!audits.Contains("perf-" + index)) db.Add(new WebRequestAuditLog { RequestMethod = "GET", RequestPath = "/performance-fixture/" + index,
                    TraceId = "perf-" + index, StartedAt = DateTime.Now.AddSeconds(index - count), EndedAt = DateTime.Now.AddSeconds(index - count), CreatedAt = DateTime.Now, StatusCode = 200 });
            }
            await db.SaveChangesAsync();
        }
    }

    /// <summary>均匀覆盖三十天，保留真实毫秒节拍和同条码复测。</summary>
    private static DateTime CreatedAt(int index, int count) => Start.AddDays((index - 1) % 30).AddHours(8).AddMilliseconds((index - 1) / 30 * 1500L);

    /// <summary>同来源身份的检测、量测、业务接口、路由和落格组成一票完整事实。</summary>
    private static ParcelProcessingRecord[] Records(int index, DateTime at) {
        var stages = new[] { ParcelProcessingStage.Detected, ParcelProcessingStage.DwsReceived, ParcelProcessingStage.DwsBound,
            ParcelProcessingStage.ScanUploaded, ParcelProcessingStage.ScanUploaded, ParcelProcessingStage.ChuteAssigned,
            ParcelProcessingStage.SorterDispatched, ParcelProcessingStage.SortingCompleted, ParcelProcessingStage.LandingReported };
        return stages.Select((stage, order) => {
            var recordId = "perf-" + index + "-" + order;
            var offset = order * 100 + index % 100;
            var category = order == 4 ? "chute-assignment" : stage == ParcelProcessingStage.LandingReported ? "landing" : "scan-upload";
            var payload = JsonSerializer.Serialize(new { kind = "provider-call", name = "PerformanceFixture", category, outcomeLevel = "transport", outcome = "HTTP成功", request = new string('x', 2048), response = "{\"isSuccess\":true}" });
            return new ParcelProcessingRecord { Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recordId))), RecordId = recordId,
                SourceInstanceId = Source, SourceRunId = "run-" + index % 4, SourceParcelId = index, ParcelId = BaseId + index,
                WorkstationName = "性能工作台 " + index % 4, Stage = stage, PartitionTime = at, RecordedAt = at.AddMilliseconds(offset),
                OccurredAt = order == 0 ? at : at.AddMilliseconds(offset), IsSuccess = true, HasReliableTimestamp = true,
                Barcode = "PERF-" + (index % 1000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                MessageIdentity = "measurement-" + index, FinalSourceParcelId = stage == ParcelProcessingStage.DwsBound ? index : null,
                WeightGrams = 1000 + index % 3, LengthMm = 300, WidthMm = 200, HeightMm = 100, VolumeMm3 = 6000000,
                ReceivedAt = at.AddMilliseconds(100 + index % 100), MeasuredAt = at.AddMilliseconds(80),
                RequestAt = stage is ParcelProcessingStage.ScanUploaded or ParcelProcessingStage.LandingReported ? at.AddMilliseconds(offset) : null,
                ResponseAt = stage is ParcelProcessingStage.ScanUploaded or ParcelProcessingStage.LandingReported ? at.AddMilliseconds(offset + 20) : null,
                Provider = "PerformanceFixture", RawPayload = payload, TargetChuteCode = "0013", ActualChuteCode = "0013" };
        }).ToArray();
    }
}
