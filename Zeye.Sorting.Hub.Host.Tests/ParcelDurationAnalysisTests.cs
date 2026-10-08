using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>使用真实关系数据库和物理日分表验证阶段端点、接口尝试及缓存总体。</summary>
public sealed class ParcelDurationAnalysisTests {
    /// <summary>四种提供器均在分表内裁剪正文，阶段查询不能加载LOB，接口只加载有界元数据。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void FactProjectionsTranslateWithoutMaterializingFullMessages(string provider) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        // 跨提供器翻译测试的临时服务容器独立于EF生产服务缓存，避免测试顺序累计触发全局警告。
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""]);
        using var streaming = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""], streaming: true);
        Assert.Equal(db.Database.CreateExecutionStrategy().RetriesOnFailure, read.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.False(streaming.Database.CreateExecutionStrategy().RetriesOnFailure);
        var at = new DateTime(2026, 10, 1);
        var facts = read.Query(["202610"], nameof(ParcelProcessingRecord.PartitionTime), at, at.AddDays(1), false);
        var stageSql = ParcelDurationAnalysisReader.BuildFactProjection(facts, [ParcelProcessingStage.DwsBound], false, "source-a").ToQueryString();
        Assert.DoesNotContain("UNION", stageSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[.](?:`|\\\"|\\[)?(?:RawPayload|ErrorMessage)(?:`|\\\"|\\])?", stageSql);
        var callSql = ParcelDurationAnalysisReader.BuildFactProjection(facts, [ParcelProcessingStage.ScanUploaded], true, "source-a").ToQueryString();
        Assert.DoesNotContain("UNION", callSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2048", callSql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.RequestBody), callSql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.ResponseBody), callSql, StringComparison.Ordinal);
    }

    /// <summary>DWS使用来源检测和首次关联时间，晚到事实仍按首次入库归组，缺失及不可靠时间不补零。</summary>
    [Fact]
    public async Task DwsUsesFirstAcquisitionAcrossDatesAndPreservesPrecision() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 23, 59, 59).AddMilliseconds(800);
        var first = Fact(1, "detected-1", at) with { PartitionTime = at.AddMilliseconds(100) };
        var dws = Stage(first, "dws-first", ParcelProcessingStage.DwsBound, at.AddTicks(3333333)) with { MeasuredAt = at.AddHours(-1), FinalSourceParcelId = 1 };
        await SeedAsync(database, first, [dws, dws with { Key = "dws-again", RecordId = "dws-again", OccurredAt = at.AddDays(1) },
            Stage(first, "target", ParcelProcessingStage.ChuteAssigned, dws.OccurredAt.AddTicks(500001)),
            Stage(first, "dispatch", ParcelProcessingStage.SorterDispatched, dws.OccurredAt.AddSeconds(1)),
            Stage(first, "complete", ParcelProcessingStage.SortingCompleted, dws.OccurredAt.AddSeconds(2))]);
        var missing = Fact(2, "detected-2", at);
        await SeedAsync(database, missing, [Stage(missing, "unbound", ParcelProcessingStage.DwsReceived, at.AddSeconds(1)) with { ParcelId = null },
            Stage(missing, "foreign-session", ParcelProcessingStage.DwsBound, at.AddSeconds(1)) with { SourceRunId = "another-run", FinalSourceParcelId = 2 }]);
        var zero = Fact(3, "detected-3", at);
        await SeedAsync(database, zero, [Stage(zero, "dws-zero", ParcelProcessingStage.DwsBound, at) with { FinalSourceParcelId = 3 }]);
        var unreliable = Fact(4, "detected-4", at);
        await SeedAsync(database, unreliable, [Stage(unreliable, "dws-unreliable", ParcelProcessingStage.DwsBound, at.AddSeconds(1)) with { HasReliableTimestamp = false, FinalSourceParcelId = 4 }]);
        var badStart = Fact(5, "detected-5", at) with { HasReliableTimestamp = false };
        await SeedAsync(database, badStart, [Stage(badStart, "dws-start-unreliable", ParcelProcessingStage.DwsBound, at.AddSeconds(1)) with { HasReliableTimestamp = true, FinalSourceParcelId = 5 }]);
        var report = await DurationAsync(database, Request(at, "dws"));
        Assert.Equal(5, report.ObservedCount);
        Assert.Equal(2, report.SampleCount);
        Assert.Equal(3, report.UnavailableCount);
        Assert.Equal(333.3333m, report.MaximumMilliseconds);
        Assert.Equal(0m, report.MinimumMilliseconds);
        Assert.Equal(166.66665m, report.MedianMilliseconds);
        Assert.Equal(at, report.Items[0].StartedAt);
        Assert.Equal(dws.OccurredAt, report.Items[0].EndedAt);
        Assert.Equal(50.0001m, Assert.Single((await DurationAsync(database, Request(at, "routing"))).Items).Milliseconds);
        Assert.Equal(1000m, Assert.Single((await DurationAsync(database, Request(at, "sorting"))).Items).Milliseconds);
        Assert.Empty((await DurationAsync(database, Request(at, "dws") with { SourceInstanceId = "missing-source" })).Items);
    }

    /// <summary>同一次诊断和HTTP不重复计数，重试分开；业务失败不能被HTTP成功替换。</summary>
    [Fact]
    public async Task InterfaceAttemptsUseTransportOnceAndKeepFailuresAndRetries() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact(10, "detected", at);
        var records = new List<ParcelProcessingRecord>();
        foreach (var attempt in new[] { 1, 2 }) {
            var start = at.AddSeconds(attempt);
            records.Add(Operation(first, "start-" + attempt, start, "目标格口分配", "started", attempt));
            records.Add(Stage(first, "http-" + attempt, ParcelProcessingStage.ScanUploaded, start.AddMilliseconds(50)) with {
                IsSuccess = null, RequestAt = start.AddTicks(10001), ResponseAt = start.AddTicks(500006), ElapsedMilliseconds = 50,
                RequestUrl = "https://user:password@internal.invalid/chute?token=hidden#secret", Provider = "ProviderA",
                RawPayload = JsonSerializer.Serialize(new { kind = "provider-call", name = "ProviderA", category = "assignment", outcomeLevel = "transport", request = new string('x', 10000), response = new { businessAccepted = true } })
            });
            records.Add(Operation(first, "end-" + attempt, start.AddMilliseconds(60), "目标格口分配", attempt == 1 ? "failed" : "accepted", attempt));
            records.Add(Operation(first, "late-" + attempt, start.AddSeconds(2), "目标格口分配", "late-accepted", attempt));
        }
        await SeedAsync(database, first, records);
        var report = await DurationAsync(database, Request(at, "chute-request"));
        Assert.Equal(2, report.ObservedCount);
        Assert.Equal(2, report.SampleCount);
        Assert.Equal(1, report.ParcelCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(0, report.UnknownResultCount);
        Assert.All(report.Items, sample => Assert.Equal(49.0005m, sample.Milliseconds));
        Assert.Equal([1, 2], report.Items.Select(sample => sample.AttemptNumber).Order());
        var api = Assert.Single(report.Interfaces);
        Assert.Equal("https://internal.invalid/chute", api.RequestUrl);
        Assert.Equal(2, api.Count);
        Assert.Equal(1, api.FailedCount);
        Assert.Empty((await DurationAsync(database, Request(at, "scan-upload"))).Items);
        Assert.Empty((await DurationAsync(database, Request(at, "other-api"))).Items);
    }

    /// <summary>明确来源上报的零耗时有效，缺失、倒序、不可靠及禁用配置不制造样本。</summary>
    [Fact]
    public async Task MissingResponsesAndSkippedOperationsDoNotBecomeZeroDuration() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact(20, "detected", at);
        var valid = Stage(first, "reported-zero", ParcelProcessingStage.ScanUploaded, at.AddSeconds(1)) with {
            IsSuccess = null, ElapsedMilliseconds = 0, RawPayload = "{\"kind\":\"provider-call\",\"name\":\"ProviderA\",\"category\":\"scan-upload\",\"outcomeLevel\":\"transport\"}"
        };
        await SeedAsync(database, first, [valid,
            valid with { Key = "missing", RecordId = "missing", ElapsedMilliseconds = null },
            valid with { Key = "negative", RecordId = "negative", RequestAt = at.AddSeconds(2), ResponseAt = at.AddSeconds(1) },
            valid with { Key = "unreliable", RecordId = "unreliable", HasReliableTimestamp = false },
            Operation(first, "disabled-start", at.AddSeconds(5), "ProviderA 扫描上传", "started", 1) with { ErrorMessage = "Scan upload disabled by config." },
            Operation(first, "disabled-end", at.AddSeconds(5), "ProviderA 扫描上传", "completed", 1) with { ErrorMessage = "Scan upload disabled by config.", ElapsedMilliseconds = 0 }]);
        var report = await DurationAsync(database, Request(at, "scan-upload"));
        Assert.Equal(5, report.ObservedCount);
        Assert.Equal(4, report.UnavailableCount);
        var sample = Assert.Single(report.Items);
        Assert.Equal(0m, sample.Milliseconds);
        Assert.Equal("来源上报", sample.TimingSource);
        Assert.Null(sample.StartedAt);
        Assert.Null(sample.EndedAt);
        Assert.Null(sample.IsSuccess);
        Assert.Equal(1, report.UnknownResultCount);
    }

    /// <summary>无HTTP事实的真实调用窗口保留亚毫秒精度，并区分扫描、落格和图片上传。</summary>
    [Theory]
    [InlineData("scan-upload", "ProviderA 扫描上传")]
    [InlineData("landing-report", "ProviderA 落格回传")]
    [InlineData("image-upload", "ProviderA 图片上传")]
    public async Task OperationsWithoutTransportRetainExplicitBusinessType(string type, string operation) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact(30, "detected", at);
        await SeedAsync(database, first, [Operation(first, "start", at, operation, "started", 1),
            Operation(first, "end", at.AddTicks(1234), operation, "accepted", 1),
            Operation(first, "late", at.AddSeconds(5), operation, "late-accepted", 1)]);
        var report = await DurationAsync(database, Request(at, type));
        Assert.Equal(1, report.ObservedCount);
        Assert.Equal(.1234m, Assert.Single(report.Items).Milliseconds);
        Assert.Equal(true, report.Items[0].IsSuccess);
    }

    /// <summary>既有Owned接口跨分表查询，按业务分类返回，时间缺失不补零且不重复已保存的HTTP。</summary>
    [Fact]
    public async Task LegacyRequestsRemainCompatibleAndDeduplicateMatchingTransport() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 23, 59, 59);
        var first = Fact(40, "detected", at);
        var start = at.AddSeconds(2);
        var end = start.AddTicks(22222);
        var transport = Stage(first, "http", ParcelProcessingStage.LandingReported, end) with {
            RequestAt = start, ResponseAt = end, Provider = "ProviderA", RequestUrl = "https://internal.invalid/landing",
            RawPayload = "{\"kind\":\"provider-call\",\"name\":\"ProviderA\",\"category\":\"landing\"}"
        };
        await SeedAsync(database, first, [transport], [
            new ApiRequestInfo { ApiType = ApiRequestType.DischargeReport, RequestStatus = ApiRequestStatus.Success, RequestUrl = transport.RequestUrl, RequestTime = start, ResponseTime = end },
            new ApiRequestInfo { ApiType = ApiRequestType.DischargeReport, RequestStatus = ApiRequestStatus.Failed, RequestUrl = "https://internal.invalid/second", RequestTime = start.AddSeconds(1), ResponseTime = end.AddSeconds(1) },
            new ApiRequestInfo { ApiType = ApiRequestType.DischargeReport, RequestStatus = ApiRequestStatus.Failed, RequestUrl = "https://internal.invalid/second", RequestTime = start.AddSeconds(1), ResponseTime = end.AddSeconds(1), QueryParams = "retry=2" },
            new ApiRequestInfo { ApiType = ApiRequestType.DischargeReport, RequestStatus = ApiRequestStatus.Success, RequestUrl = "https://internal.invalid/missing", RequestTime = start },
            new ApiRequestInfo { ApiType = ApiRequestType.RequestChute, RequestStatus = ApiRequestStatus.Success, RequestUrl = "https://internal.invalid/chute", RequestTime = start, ResponseTime = end },
            new ApiRequestInfo { ApiType = ApiRequestType.LockChute, RequestStatus = ApiRequestStatus.Success, RequestUrl = "https://internal.invalid/lock", RequestTime = start, ResponseTime = end }
        ]);
        var report = await DurationAsync(database, Request(at, "landing-report"));
        Assert.Equal(4, report.ObservedCount);
        Assert.Equal(3, report.SampleCount);
        Assert.Equal(1, report.UnavailableCount);
        Assert.Equal(2, report.FailedCount);
        Assert.All(report.Items, sample => Assert.Equal(2.2222m, sample.Milliseconds));
        Assert.Single((await DurationAsync(database, Request(at, "chute-request"))).Items);
        Assert.Single((await DurationAsync(database, Request(at, "other-api"))).Items);
    }

    /// <summary>完整总体分位数不受分页和区间影响，刷新快照才纳入新事实。</summary>
    [Fact]
    public async Task QuantilesAndCacheRemainStableAcrossPagingAndBucketFilters() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact(50, "detected", at);
        var calls = Enumerable.Range(0, 41).Select(index => Stage(first, "http-" + index, ParcelProcessingStage.ImageUploaded, at.AddSeconds(index)) with {
            IsSuccess = null, RequestAt = at.AddSeconds(index), ResponseAt = at.AddSeconds(index).AddMilliseconds(index * 100), RequestUrl = "https://internal.invalid/image"
        }).ToArray();
        await SeedAsync(database, first, calls);
        using var cache = new ParcelDurationAnalysisCache();
        var reader = Reader(database, cache);
        var request = Request(at, "image-upload");
        var report = (await reader.ReadAsync(request, default)).DurationAnalysis!;
        Assert.Equal(41, report.SampleCount);
        Assert.Equal(2000m, report.MedianMilliseconds);
        Assert.Equal(3800m, report.P95Milliseconds);
        Assert.Equal(20, report.Items.Count);
        var filtered = (await reader.ReadAsync(request with { PageNumber = 2, MinimumMilliseconds = 1000, MaximumMilliseconds = 4000 }, default)).DurationAnalysis!;
        Assert.Equal(report.GeneratedAt, filtered.GeneratedAt);
        Assert.Equal(41, filtered.SampleCount);
        Assert.Equal(30, filtered.FilteredCount);
        Assert.Equal(10, filtered.Items.Count);
        Assert.Equal(report.MedianMilliseconds, filtered.MedianMilliseconds);
        Assert.Equal(report.P95Milliseconds, filtered.P95Milliseconds);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            db.Add(Stage(first, "new-http", ParcelProcessingStage.ImageUploaded, at.AddHours(1)) with { ElapsedMilliseconds = 9000 });
            await db.SaveChangesAsync();
        }
        Assert.Equal(41, (await reader.ReadAsync(request, default)).DurationAnalysis!.SampleCount);
        Assert.Equal(42, (await reader.ReadAsync(request with { RefreshDurationSnapshot = true }, default)).DurationAnalysis!.SampleCount);
    }

    /// <summary>各接口只缓存自身总体，跨类型不误去重；刷新只重新读取当前所选类型。</summary>
    [Fact]
    public async Task ApiTypesCacheIndependentlyAndRefreshWithoutCrossTypeDeduplication() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        var first = Fact(60, "shared-detected", at);
        var transport = Stage(first, "shared-scan", ParcelProcessingStage.ScanUploaded, at.AddMilliseconds(10)) with {
            RequestAt = at, ResponseAt = at.AddMilliseconds(10), RequestUrl = "https://internal.invalid/shared",
            RawPayload = "{\"kind\":\"provider-call\",\"category\":\"scan-upload\",\"name\":\"ProviderA\"}"
        };
        var image = Stage(first, "shared-image", ParcelProcessingStage.ImageUploaded, at.AddSeconds(1)) with { ElapsedMilliseconds = 20 };
        await SeedAsync(database, first, [transport, image], [
            new ApiRequestInfo { ApiType = ApiRequestType.ScanResult, RequestStatus = ApiRequestStatus.Success, RequestUrl = transport.RequestUrl, RequestTime = at, ResponseTime = transport.ResponseAt },
            new ApiRequestInfo { ApiType = ApiRequestType.RequestChute, RequestStatus = ApiRequestStatus.Failed, RequestUrl = transport.RequestUrl, RequestTime = at, ResponseTime = transport.ResponseAt }
        ]);
        using var cache = new ParcelDurationAnalysisCache();
        var reader = Reader(database, cache);
        capture.Commands.Clear();
        var scan = (await reader.ReadAsync(Request(at, "scan-upload"), default)).DurationAnalysis!;
        Assert.Equal(1, scan.SampleCount);
        Assert.Equal(10m, Assert.Single(scan.Items).Milliseconds);
        Assert.NotEmpty(capture.Commands);
        capture.Commands.Clear();
        var chute = (await reader.ReadAsync(Request(at, "chute-request"), default)).DurationAnalysis!;
        Assert.Equal(1, chute.SampleCount);
        Assert.Equal(1, chute.FailedCount);
        Assert.NotEmpty(capture.Commands);
        Assert.Equal(1, (await reader.ReadAsync(Request(at, "image-upload"), default)).DurationAnalysis!.SampleCount);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            db.Add(image with { Key = "shared-new-image", RecordId = "shared-new-image", ElapsedMilliseconds = 30 });
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, (await reader.ReadAsync(Request(at, "image-upload"), default)).DurationAnalysis!.SampleCount);
        await reader.ReadAsync(Request(at, "scan-upload") with { RefreshDurationSnapshot = true }, default);
        capture.Commands.Clear();
        var refreshedImage = (await reader.ReadAsync(Request(at, "image-upload"), default)).DurationAnalysis!;
        Assert.Equal(1, refreshedImage.SampleCount);
        Assert.Empty(capture.Commands);
        Assert.Equal(2, (await reader.ReadAsync(Request(at, "image-upload") with { RefreshDurationSnapshot = true }, default)).DurationAnalysis!.SampleCount);
    }

    /// <summary>生成明确来源、运行会话和真实事件时间。</summary>
    private static ParcelProcessingRecord Fact(long id, string key, DateTime at) => new() {
        Key = key, RecordId = key, ParcelId = id, SourceParcelId = id, SourceInstanceId = "source-a", SourceRunId = "run-a",
        Stage = ParcelProcessingStage.Detected, OccurredAt = at, PartitionTime = at, RecordedAt = at,
        Barcode = "PKG-" + id, WorkstationName = "工作台A", IsSuccess = true, HasReliableTimestamp = true
    };

    /// <summary>阶段继承首次入库锚点，不用发生日替换分表。</summary>
    private static ParcelProcessingRecord Stage(ParcelProcessingRecord first, string key, ParcelProcessingStage stage, DateTime at) => first with {
        Key = key, RecordId = key, Stage = stage, OccurredAt = at
    };

    /// <summary>来源协议将尝试身份和序号保存在诊断详情中，顶层领域尝试号可能仍为默认值。</summary>
    private static ParcelProcessingRecord Operation(ParcelProcessingRecord first, string key, DateTime at, string operation, string outcome, int attempt) => Stage(first, key, ParcelProcessingStage.ScanUploaded, at) with {
        IsSuccess = null,
        RawPayload = JsonSerializer.Serialize(new { kind = "provider-attempt", category = "ProviderA", name = operation, outcome,
            detail = JsonSerializer.Serialize(new { operationId = "op-" + operation, attemptId = "attempt-" + attempt, attemptNumber = attempt, operation, outcome }) })
    };

    /// <summary>写入真实物理分表和Owned请求；不使用模拟查询结果。</summary>
    private static async Task SeedAsync(RelationalParcelTestDatabase database, ParcelProcessingRecord first, IEnumerable<ParcelProcessingRecord> records, IEnumerable<ApiRequestInfo>? apiRequests = null) {
        var facts = records.Prepend(first).ToArray();
        var parcel = Parcel.CreateDetected(first.ParcelId!.Value, first, first.PartitionTime);
        parcel.ApplyProcessingRecords(facts.Where(record => record.SourceRunId == first.SourceRunId && record.ParcelId == first.ParcelId).ToArray());
        foreach (var api in apiRequests ?? []) parcel.AddApiRequest(api);
        Assert.True((await database.Parcels.AddAsync(parcel, default)).IsSuccess);
        await using var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(first.PartitionTime).Suffix, default);
        db.AddRange(facts);
        await db.SaveChangesAsync();
    }

    /// <summary>同生产保持EF Core只读分表和查询预算。</summary>
    private static ParcelAnalysisReadService Reader(RelationalParcelTestDatabase database, ParcelDurationAnalysisCache? cache = null) => new(
        database.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), database.Partitions, cache);

    /// <summary>生成首次入库单日总体，接口发生时间允许跨日。</summary>
    private static ParcelAnalysisRequest Request(DateTime at, string type) => new() { View = "duration", DurationType = type, FromDate = at.Date, ToDate = at.Date };

    /// <summary>读取新增类型并断言契约存在。</summary>
    private static async Task<ParcelDurationAnalysisResponse> DurationAsync(RelationalParcelTestDatabase database, ParcelAnalysisRequest request) {
        var response = await Reader(database).ReadAsync(request, default);
        Assert.NotNull(response.DurationAnalysis);
        if (ParcelDurationAnalysisReader.Types[request.DurationType].Calls) {
            var backfill = new ParcelDurationBackfillService(database.Factory, database.Partitions);
            while (await backfill.RunBatchAsync(default) > 0) { }
            var indexed = (await Reader(database).ReadAsync(request, default)).DurationAnalysis!;
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(response.DurationAnalysis), JsonSerializer.SerializeToElement(indexed with { GeneratedAt = response.DurationAnalysis.GeneratedAt })));
            var projection = new ParcelDurationCallProjectionService(database.Factory, database.Partitions);
            while (await projection.RunBatchAsync(default) > 0) { }
            var canonical = (await Reader(database).ReadAsync(request, default)).DurationAnalysis!;
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(indexed), JsonSerializer.SerializeToElement(canonical with { GeneratedAt = indexed.GeneratedAt })));
            return canonical;
        }
        return response.DurationAnalysis;
    }
}
