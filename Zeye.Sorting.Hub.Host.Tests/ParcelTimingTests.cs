using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>通过真实SQLite物理分表验证时序锚点、稳定邻近窗口与只读接口。</summary>
public sealed class ParcelTimingTests {
    /// <summary>跨日分表以同一扫码时间及64位Id稳定选取前后各5票，锚点只出现一次。</summary>
    [Fact]
    public async Task TimingIncludesAnchorAndFiveOnEachSideAcrossPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var scan = new DateTime(2026, 8, 1, 23, 59, 59).AddTicks(1234567);
        for (var index = 0; index < 15; index++)
            await SaveAsync(database, 9007199254741000L + index, scan, "DUP", scan.AddDays(index % 2));
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        var result = await reader.ReadAsync(9007199254741007L, default);
        Assert.NotNull(result);
        Assert.Equal(5, result.BeforeCount);
        Assert.Equal(5, result.AfterCount);
        Assert.Equal(11, result.Items.Count);
        Assert.Equal(Enumerable.Range(2, 11).Select(index => (9007199254741000L + index).ToString(CultureInfo.InvariantCulture)), result.Items.Select(parcel => parcel.Id));
        Assert.Single(result.Items.Where(parcel => parcel.Id == result.AnchorId));
        Assert.All(result.Items, parcel => Assert.Equal(scan, parcel.ScannedTime));
        Assert.All(result.Items, parcel => Assert.Single(parcel.ProcessingRecords));
    }

    /// <summary>数据不足时返回真实票数；不存在的目标返回空值。</summary>
    [Fact]
    public async Task TimingAtBoundaryReturnsAvailableParcelsWithoutPadding() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 7, 1, 10, 0, 0);
        for (var index = 1; index <= 3; index++) await SaveAsync(database, index, at.AddSeconds(index), "P" + index);
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        var first = await reader.ReadAsync(1, default);
        Assert.NotNull(first);
        Assert.Equal(0, first.BeforeCount);
        Assert.Equal(2, first.AfterCount);
        Assert.Equal(["1", "2", "3"], first.Items.Select(parcel => parcel.Id));
        var last = await reader.ReadAsync(3, default);
        Assert.NotNull(last);
        Assert.Equal(2, last.BeforeCount);
        Assert.Equal(0, last.AfterCount);
        Assert.Null(await reader.ReadAsync(99, default));
    }

    /// <summary>补传后的Hub首次入库时间与来源检测时间分别返回，不改变邻近票的业务时间排序。</summary>
    [Fact]
    public async Task TimingReturnsHubRegistrationSeparatelyFromSourceDetection() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var detected = new DateTime(2026, 10, 6, 10, 24, 28).AddTicks(8471234);
        var registered = detected.AddDays(2).AddMilliseconds(500);
        await SaveAsync(database, 41, detected.AddMilliseconds(-1), "BEFORE", registered.AddSeconds(1));
        await SaveAsync(database, 42, detected, "ANCHOR", registered);
        await SaveAsync(database, 43, detected.AddMilliseconds(1), "AFTER", registered.AddSeconds(-1));
        await using var app = await BuildAppAsync(database);
        using var client = app.GetTestClient();
        var result = await client.GetFromJsonAsync<ParcelTimingResponse>("/api/parcels/timing/42");
        Assert.Equal(["41", "42", "43"], result!.Items.Select(parcel => parcel.Id));
        var parcel = Assert.Single(result.Items, parcel => parcel.Id == "42");
        Assert.Equal(detected, parcel.DetectedTime);
        Assert.Equal(detected, parcel.ScannedTime);
        Assert.Equal(registered, parcel.CreatedTime);
        Assert.NotEqual(parcel.CreatedTime, parcel.DetectedTime);
        var candidates = await client.GetFromJsonAsync<ParcelTimingCandidatesResponse>("/api/parcels/timing/candidates?query=ANCHOR&searchBy=barcode");
        Assert.Equal(registered, Assert.Single(candidates!.Items).CreatedTime);
    }

    /// <summary>历史条码精确匹配并保留重复候选，数字条码和Id碰撞也必须由用户选择。</summary>
    [Fact]
    public async Task CandidateLookupIsExactHistoricalPagedAndPreservesNumericAmbiguity() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2020, 1, 1, 10, 0, 0);
        for (var index = 1; index <= 23; index++) await SaveAsync(database, index, at.AddMilliseconds(index), "DUP");
        await SaveAsync(database, 24, at.AddDays(1), "DUP-SUFFIX");
        await SaveAsync(database, 25, at.AddDays(1).AddSeconds(1), "1");
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        var first = await reader.SearchAsync(" DUP ", "barcode", 1, default);
        Assert.Equal(23, first.TotalCount);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal("23", first.Items[0].Id);
        var second = await reader.SearchAsync("DUP", "barcode", 2, default);
        Assert.Equal(["3", "2", "1"], second.Items.Select(parcel => parcel.Id));
        var numeric = await reader.SearchAsync("1", "auto", 1, default);
        Assert.Equal(2, numeric.TotalCount);
        Assert.Equal(["25", "1"], numeric.Items.Select(parcel => parcel.Id));
        Assert.Single((await reader.SearchAsync("1", "id", 1, default)).Items);
        Assert.Equal("25", Assert.Single((await reader.SearchAsync("1", "barcode", 1, default)).Items).Id);
        Assert.Empty((await reader.SearchAsync("DU", "barcode", 1, default)).Items);
    }

    /// <summary>只读接口保留64位字符串身份、原始时间精度、历史请求时间，并隔离其他来源事实。</summary>
    [Fact]
    public async Task TimingApiPreservesPrecisionAndScopesFactsWithoutReturningBodies() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        const long id = 9223372036854775806;
        var at = new DateTime(2026, 9, 1, 10, 0, 0).AddTicks(1234567);
        var fact = Fact(id, at, "PKG") with { ErrorMessage = "{\"error\":\"格口不可用\"}" };
        var call = fact with {
            Key = "chute-call", RecordId = "chute-call", Stage = ParcelProcessingStage.ScanUploaded,
            OccurredAt = at.AddMilliseconds(125), RequestAt = at.AddMilliseconds(100), ResponseAt = at.AddMilliseconds(125),
            RawPayload = "{\"kind\":\"provider-attempt\",\"operation\":\"请求格口\",\"outcome\":\"completed\",\"detail\":\"{\\\"operationId\\\":\\\"op1\\\"}\",\"response\":\"{\\\"IsAssigned\\\":true,\\\"secret\\\":\\\"hidden\\\"}\",\"requestBody\":\"hidden\"}",
            ErrorMessage = "{\"operationId\":\"op1\",\"requestBody\":\"hidden\"}", RequestBody = "hidden", ResponseBody = "hidden"
        };
        var parcel = Parcel.CreateDetected(id, fact, at);
        parcel.ApplyProcessingRecords([fact, call]);
        parcel.AddApiRequest(new ApiRequestInfo { ApiType = ApiRequestType.RequestChute, RequestStatus = ApiRequestStatus.Success,
            RequestUrl = "https://internal.invalid/chute", RequestTime = at.AddMilliseconds(100), ResponseTime = at.AddMilliseconds(125), RequestBody = "hidden", ElapsedMilliseconds = 25 });
        Assert.True((await database.Parcels.AddAsync(parcel, default)).IsSuccess);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            db.AddRange(fact, call, fact with { Key = "foreign", RecordId = "foreign", SourceInstanceId = "other-source" });
            await db.SaveChangesAsync();
        }
        await using var app = await BuildAppAsync(database);
        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/api/parcels/timing/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(".1234567", text);
        Assert.DoesNotContain("hidden", text);
        var result = await response.Content.ReadFromJsonAsync<ParcelTimingResponse>();
        var item = Assert.Single(result!.Items);
        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), item.Id);
        Assert.Equal(2, item.ProcessingRecords.Count);
        Assert.DoesNotContain(item.ProcessingRecords, record => record.RecordId == "foreign");
        var request = Assert.Single(item.ApiRequests);
        Assert.Equal(25m, (request.ResponseTime!.Value - request.RequestTime).Ticks / 10000m);
        var timingCall = Assert.Single(item.ProcessingRecords.Where(record => record.RecordId == "chute-call"));
        Assert.Equal(fact.ErrorMessage, Assert.Single(item.ProcessingRecords.Where(record => record.RecordId == fact.RecordId)).ErrorMessage);
        using var errorDiagnostic = JsonDocument.Parse(timingCall.ErrorMessage!);
        Assert.Equal("op1", errorDiagnostic.RootElement.GetProperty("operationId").GetString());
        using var diagnostic = JsonDocument.Parse(timingCall.RawPayload!);
        Assert.True(diagnostic.RootElement.GetProperty("response").GetProperty("IsAssigned").GetBoolean());
        Assert.Equal("op1", diagnostic.RootElement.GetProperty("detail").GetProperty("operationId").GetString());
    }

    /// <summary>Fusion调用开始的空响应及非JSON正文不能丢失操作身份、开始/结束状态和归组标识。</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData(" ", "plain-text response")]
    [InlineData("", "{\"IsAssigned\":true,\"secret\":\"hidden\"}")]
    [InlineData("", "{\"IsAssigned\":true,\"secret\":\"")]
    public async Task TimingPreservesFusionCallsWhenResponseIsEmptyTextOrTruncated(string startedResponse, string completedResponse) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 6, 10, 24, 31).AddMilliseconds(109);
        var fact = Fact(42, at, "FUSION-CALLS");
        var records = new List<ParcelProcessingRecord> { fact };
        foreach (var operation in new[] { "扫描上传", "目标格口分配", "落格回传" }) {
            foreach (var outcome in new[] { "started", "completed" }) {
                var key = operation + "-" + outcome;
                var detail = JsonSerializer.Serialize(new { operationId = operation, attemptId = operation + "-1", attemptNumber = 1, operation, outcome });
                records.Add(fact with {
                    Key = key, RecordId = key, Stage = ParcelProcessingStage.ScanUploaded,
                    OccurredAt = at.AddMilliseconds(outcome == "started" ? 146 : 155),
                    RawPayload = JsonSerializer.Serialize(new {
                        kind = "provider-attempt", name = operation, category = "EverydayChainHub", outcome,
                        detail, request = "hidden", response = outcome == "started" ? startedResponse : completedResponse,
                        outcomeLevel = "operation"
                    }),
                    ErrorMessage = detail
                });
            }
        }
        var parcel = Parcel.CreateDetected(42, fact, at);
        parcel.ApplyProcessingRecords(records);
        Assert.True((await database.Parcels.AddAsync(parcel, default)).IsSuccess);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            db.AddRange(records);
            await db.SaveChangesAsync();
        }
        var result = await new ParcelTimingReadService(database.Factory, database.Partitions).ReadAsync(42, default);
        var timingRecords = Assert.Single(result!.Items).ProcessingRecords;
        Assert.Equal(records.Count, timingRecords.Count);
        foreach (var record in timingRecords.Where(record => record.Stage == (int)ParcelProcessingStage.ScanUploaded)) {
            Assert.NotNull(record.RawPayload);
            Assert.DoesNotContain("hidden", record.RawPayload);
            Assert.DoesNotContain("plain-text response", record.RawPayload);
            using var diagnostic = JsonDocument.Parse(record.RawPayload);
            var root = diagnostic.RootElement;
            var operation = root.GetProperty("name").GetString();
            Assert.Equal("provider-attempt", root.GetProperty("kind").GetString());
            Assert.Equal(record.RecordId.EndsWith("-started", StringComparison.Ordinal) ? "started" : "completed", root.GetProperty("outcome").GetString());
            Assert.Equal(operation, root.GetProperty("detail").GetProperty("operationId").GetString());
            Assert.Equal(operation + "-1", root.GetProperty("detail").GetProperty("attemptId").GetString());
            Assert.Equal(operation, root.GetProperty("detail").GetProperty("operation").GetString());
        }
    }

    /// <summary>非法搜索及目标编号返回400，不存在的包裹返回404。</summary>
    [Fact]
    public async Task TimingApiRejectsInvalidQueriesAndMissingAnchors() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var app = await BuildAppAsync(database);
        using var client = app.GetTestClient();
        foreach (var path in new[] { "/api/parcels/timing/candidates", "/api/parcels/timing/candidates?query=x&searchBy=unknown",
            "/api/parcels/timing/candidates?query=9223372036854775808&searchBy=id", "/api/parcels/timing/candidates?query=x&pageNumber=0", "/api/parcels/timing/0" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/parcels/timing/123")).StatusCode);
    }

    /// <summary>在真实物理表写入包裹摘要与已绑定来源事实。</summary>
    private static async Task SaveAsync(RelationalParcelTestDatabase database, long id, DateTime scannedAt, string barcode, DateTime? registeredAt = null) {
        var createdAt = registeredAt ?? scannedAt;
        var fact = Fact(id, scannedAt, barcode) with { RecordedAt = createdAt, PartitionTime = createdAt };
        var parcel = Parcel.CreateDetected(id, fact, createdAt);
        parcel.ApplyProcessingRecords([fact]);
        Assert.True((await database.Parcels.AddAsync(parcel, default)).IsSuccess);
        await using var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(createdAt).Suffix, default);
        db.Add(fact);
        await db.SaveChangesAsync();
    }

    /// <summary>生成具有有效本地时间和明确来源身份的检测记录。</summary>
    private static ParcelProcessingRecord Fact(long id, DateTime at, string barcode) => new() {
        Key = "detected-" + id, RecordId = "detected-" + id, ParcelId = id,
        SourceInstanceId = "source-a", SourceRunId = "run-a", SourceParcelId = id,
        Stage = ParcelProcessingStage.Detected, OccurredAt = at, RecordedAt = at, PartitionTime = at,
        Barcode = barcode, WorkstationName = "工作台A", IsSuccess = true
    };

    /// <summary>用真实查询服务构建内存HTTP宿主，不使用伪造仓储结果。</summary>
    private static async Task<WebApplication> BuildAppAsync(RelationalParcelTestDatabase database) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton(database.Partitions);
        builder.Services.AddScoped<IParcelTimingReadService, ParcelTimingReadService>();
        var app = builder.Build();
        app.MapParcelTimingApis();
        await app.StartAsync();
        return app;
    }
}
