using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>生产HTTP路由与真实关系数据库的完整处理合同验收。</summary>
public sealed class FusionProcessingApiTests {
    /// <summary>真实 HTTP 报表使用已保存数据，并拒绝不完整或超出预算的日期范围。</summary>
    [Fact]
    public async Task AnalyticsHttpReturnsStoredCohortAndValidatesDates() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var stored = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0));
        Assert.Equal(HttpStatusCode.Created, stored.StatusCode);
        var date = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var response = await client.GetAsync($"/api/parcels/analytics?fromDate={date}&toDate={date}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ParcelAnalyticsResponse>();
        Assert.NotNull(report);
        Assert.Equal(1, report.DetectedCount);
        Assert.Equal(1, report.ProcessingEventCount);
        Assert.Null(report.AverageLifecycleSeconds);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parcels/analytics?fromDate=2026-09-28")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parcels/analytics?fromDate=2026-09-01&toDate=2026-11-01")).StatusCode);
    }

    /// <summary>检测、DWS、绑定、上传、路由、指令、落格、异常和图片全部阶段可保存并在详情返回。</summary>
    [Fact]
    public async Task AllProcessingStagesAreVisibleThroughRealHttpDetail() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        string? parcelId = null;
        for (var stage = 0; stage <= 10; stage++) {
            var request = Request(stage) with {
                IsSuccess = true, FinalSourceParcelId = 42, TargetChuteCode = "A01", ActualChuteCode = "A02", ExceptionCode = "ParcelSpacingViolation",
                RawPayload = "raw-device-data", WeightGrams = 1200.123m, VolumetricWeightGrams = 2400m, VolumeMm3 = null,
                ImagePath = "images/parcel-42.jpg", ImageCamera = "camera-01", ImageContentHash = "sha256-42",
                RequestBody = "request-body", ResponseBody = "response-body", MessageIdentity = "batch-scan-sequence", CorrelationId = long.MaxValue - 1,
                PreviousCreationGapMilliseconds = 15, IsSpacingViolation = true, IsAwaitingWcsDecision = false
            };
            var response = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>();
            parcelId ??= result!.ParcelId;
            Assert.Equal(parcelId, result!.ParcelId);
        }
        var detail = await client.GetStringAsync("/api/parcels/" + parcelId);
        using var json = JsonDocument.Parse(detail);
        var records = json.RootElement.GetProperty("processingRecords");
        Assert.Equal(11, records.GetArrayLength());
        Assert.Contains("raw-device-data", detail, StringComparison.Ordinal);
        Assert.Contains("request-body", detail, StringComparison.Ordinal);
        Assert.Contains("response-body", detail, StringComparison.Ordinal);
        Assert.Contains("batch-scan-sequence", detail, StringComparison.Ordinal);
        Assert.Contains((long.MaxValue - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), detail, StringComparison.Ordinal);
        Assert.Equal(1.200123m, json.RootElement.GetProperty("weight").GetDecimal());
        Assert.Equal(1, json.RootElement.GetProperty("status").GetInt32());
    }

    /// <summary>同一HTTP消息重试无副作用，内容冲突返回409；非法阶段与时区返回400。</summary>
    [Fact]
    public async Task DuplicateConflictAndInvalidFactsHaveExplicitHttpStatus() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var first = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0));
        var duplicate = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0));
        var conflict = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0) with { RawPayload = "changed-content" });
        var invalid = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(99));
        var zone = await client.PostAsync("/api/admin/parcels/processing-records", new StringContent("{\"occurredAt\":\"2026-09-28T10:00:00+08:00\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zone.StatusCode);
        var result = await duplicate.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>();
        Assert.True(result!.IsDuplicate);
    }

    /// <summary>同一过机身份的第二次检测拒绝误合并，重放复用记录身份，计数重置换会话后建立新包裹。</summary>
    [Fact]
    public async Task DetectionIdentityDistinguishesReplayFromCounterReset() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var detection = Request(0) with { Barcode = "NoRead" };

        var first = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", detection);
        var replay = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", detection);
        var reusedCounter = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", detection with { RecordId = "new-record-after-counter-reset" });
        var newRun = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", detection with { SourceRunId = "counter-reset-session" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reusedCounter.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newRun.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>();
        var duplicate = await replay.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>();
        var afterReset = await newRun.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>();
        Assert.True(duplicate!.IsDuplicate);
        Assert.Equal(original!.ParcelId, duplicate.ParcelId);
        Assert.NotEqual(original.ParcelId, afterReset!.ParcelId);
        Assert.Equal(2, await database.CountPhysicalAsync("Parcels_" + original.PartitionSuffix));
        Assert.Equal(2, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }

    /// <summary>来源身份中的首尾空白和控制字符必须在建表及落库前拒绝。</summary>
    [Fact]
    public async Task NonCanonicalSourceIdentityReturnsBadRequestWithoutReceipt() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var detection = Request(0);
        var invalid = new[] {
            detection with { RecordId = " record" },
            detection with { SourceInstanceId = "sorter " },
            detection with { SourceRunId = "run\nreset" },
            detection with { CandidateSourceParcelId = -1 }
        };
        foreach (var request in invalid) {
            var response = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }

    /// <summary>并发提交同一条消息只保存一次，返回同一中心身份。</summary>
    [Fact]
    public async Task ConcurrentDuplicateRequestsAppendOneRecord() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0))));
        Assert.Single(replies.Where(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(11, replies.Count(x => x.StatusCode == HttpStatusCode.OK));
        var suffix = (await replies[0].Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>())!.PartitionSuffix;
        Assert.Equal(1, await database.CountPhysicalAsync("Parcel_ProcessingRecords_" + suffix));
    }

    /// <summary>真实批量入口保留字符串64位编号，入队后由生产刷新服务完成分表落库。</summary>
    [Fact]
    public async Task LegacyBatchBufferPersistsLargeIdsThroughPhysicalRouting() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        var body = """{"parcels":[{"id":"9223372036854775806","parcelTimestamp":"639261864000000001","type":0,"barCodes":"BATCH-REAL","weight":1.234567,"workstationName":"batch-test","scannedTime":"2026-09-28T10:00:00","dischargeTime":"2026-09-28T10:00:03","targetChuteId":"1","actualChuteId":"2","requestStatus":1}]}""";
        var response = await client.PostAsync("/api/admin/parcels/batch-buffer", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ParcelBatchBufferedCreateResponse>();
        Assert.Equal(1, result!.AcceptedCount);
        Assert.Null(await database.Parcels.GetByIdAsync(long.MaxValue - 1, default));
        Assert.True(await app.Services.GetRequiredService<ParcelBatchWriteFlushService>().FlushOnceAsync(default));
        var parcel = await database.Parcels.GetByIdAsync(long.MaxValue - 1, default);
        Assert.NotNull(parcel);
        Assert.Equal(639261864000000001, parcel.ParcelTimestamp);
        Assert.Equal(1.234567m, parcel.Weight);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels_" + await database.Partitions.LocateAsync(parcel.Id, default)));
    }

    /// <summary>治理页面返回真实跨表计划与默认阻断决策，不伪造演练或删除结果。</summary>
    [Fact]
    public async Task CleanupHttpReturnsGuardedPhysicalPartitionCount() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(database);
        using var client = app.GetTestClient();
        await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0));
        var response = await client.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", new { createdBefore = DateTime.Now.Date.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ParcelCleanupExpiredResponse>();
        Assert.True(result!.IsBlockedByGuard);
        Assert.Equal(1, result.PlannedCount);
        Assert.Equal(0, result.ExecutedCount);
    }

    /// <summary>建立使用本地时间与明确设备会话的处理合同。</summary>
    private static ParcelProcessingRecordRequest Request(int stage) => new() {
        RecordId = "http-record-" + stage, SourceInstanceId = "fusion-http-sorter", SourceRunId = "counter-http-session", SourceParcelId = 42,
        Stage = stage, OccurredAt = DateTime.Now.Date.AddHours(10).AddSeconds(stage)
    };
}
