using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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

/// <summary>真实物理分表上的多票对比验证，覆盖顺序、量测单位、原始时间及只读预算。</summary>
public sealed class ParcelComparisonTests {
    /// <summary>只读指定身份，保留选择顺序与64位身份，不读同时间邻票，跨日事实按来源隔离。</summary>
    [Fact]
    public async Task ComparisonPreservesSelectionAndUnitsAcrossPhysicalPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 23, 59, 59).AddTicks(1234567);
        const long first = 9223372036854775804, second = 9223372036854775805, neighbour = 9223372036854775806;
        await SaveAsync(database, first, at, 1250m, 20_000_000m);
        await SaveAsync(database, neighbour, at.AddMilliseconds(1), 9000m, 90_000_000m);
        await SaveAsync(database, second, at.AddDays(1), 2500m, 30_000_000m);
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        var result = await reader.CompareAsync([second, first, second, 9], default);
        Assert.Equal([second.ToString(CultureInfo.InvariantCulture), first.ToString(CultureInfo.InvariantCulture), "9"], result.RequestedIds);
        Assert.Equal(["9"], result.MissingIds);
        Assert.Equal(result.RequestedIds.Take(2), result.Items.Select(item => item.Timing.Id));
        Assert.DoesNotContain(result.Items, item => item.Timing.Id == neighbour.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(2.5m, result.Items[0].Weight);
        Assert.Equal(1.25m, result.Items[1].Weight);
        Assert.Equal(30_000_000m, result.Items[0].Volume);
        Assert.Equal(300m, result.Items[0].Length);
        Assert.Equal("0013", result.Items[0].TargetChuteCode);
        Assert.Equal("13", result.Items[0].ActualChuteCode);
        Assert.All(result.Items, item => {
            Assert.Equal(4, item.Timing.ProcessingRecords.Count);
            Assert.DoesNotContain(item.Timing.ProcessingRecords, record => record.RecordId == "foreign");
            Assert.All(item.Timing.ProcessingRecords, record => Assert.Null(record.RawPayload));
            var call = Assert.Single(item.Timing.ApiRequests);
            Assert.Equal(999, call.ElapsedMilliseconds);
            Assert.Equal(7.0001m, (call.ResponseTime!.Value - call.RequestTime).Ticks / 10000m);
        });
    }

    /// <summary>未知量测与真实零分别返回；体积缺失时不从尺寸补算，全部缺失的包裹不生成空白票。</summary>
    [Fact]
    public async Task ComparisonKeepsMissingMeasurementsDistinctFromZero() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var at = new DateTime(2026, 10, 2, 10, 0, 0);
        await SaveAsync(database, 1, at, null, null);
        await SaveAsync(database, 2, at.AddSeconds(1), 0m, 0m);
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        var result = await reader.CompareAsync([1, 2], default);
        Assert.Null(result.Items[0].Weight);
        Assert.Null(result.Items[0].Volume);
        Assert.Equal(300m, result.Items[0].Length);
        Assert.Equal(0m, result.Items[1].Weight);
        Assert.Equal(0m, result.Items[1].Volume);
        var missing = await reader.CompareAsync([88, 99], default);
        Assert.Empty(missing.Items);
        Assert.Equal(["88", "99"], missing.MissingIds);
    }

    /// <summary>HTTP接口保留七位精度、隐藏完整报文，并限制无效身份和超额读取。</summary>
    [Fact]
    public async Task ComparisonApiValidatesBudgetAndPreservesPreciseReadOnlyContract() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        const long id = 9223372036854775806;
        var at = new DateTime(2026, 10, 2, 10, 0, 0).AddTicks(1234567);
        await SaveAsync(database, id, at, 1001m, 5_000_001m);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(database.Factory);
        builder.Services.AddSingleton(database.Partitions);
        builder.Services.AddScoped<IParcelTimingReadService, ParcelTimingReadService>();
        await using var app = builder.Build();
        app.MapParcelTimingApis();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/api/parcels/timing/compare?ids={id},11");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(".1234567", json);
        Assert.DoesNotContain("hidden", json);
        var result = await response.Content.ReadFromJsonAsync<ParcelComparisonResponse>();
        Assert.Equal(id.ToString(CultureInfo.InvariantCulture), Assert.Single(result!.Items).Timing.Id);
        Assert.Equal(1.001m, result.Items[0].Weight);
        Assert.Equal(["11"], result.MissingIds);
        foreach (var path in new[] { "", "?ids=0", "?ids=-1", "?ids=1,,2", "?ids=abc", "?ids=9223372036854775808", "?ids=1,2,3,4,5,6,7,8,9" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parcels/timing/compare" + path)).StatusCode);
        var reader = new ParcelTimingReadService(database.Factory, database.Partitions);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.CompareAsync([], default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.CompareAsync([0], default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.CompareAsync(Enumerable.Range(1, 9).Select(value => (long)value).ToArray(), default));
    }

    /// <summary>在实际分表保存检测、DWS量测、落格与历史调用，同时放入不属于该来源的事实。</summary>
    private static async Task SaveAsync(RelationalParcelTestDatabase database, long id, DateTime at, decimal? grams, decimal? volume) {
        var detected = new ParcelProcessingRecord {
            Key = "detected-" + id, RecordId = "detected-" + id, ParcelId = id,
            SourceInstanceId = "compare-source", SourceRunId = "compare-run", SourceParcelId = id,
            Stage = ParcelProcessingStage.Detected, OccurredAt = at, RecordedAt = at, PartitionTime = at,
            Barcode = "DUP", WorkstationName = "对比工作台", IsSuccess = true, RawPayload = "hidden"
        };
        var dws = detected with { Key = "dws-" + id, RecordId = "dws-" + id, Stage = ParcelProcessingStage.DwsBound,
            OccurredAt = at.AddMilliseconds(10), WeightGrams = grams, LengthMm = 300m, WidthMm = 200m, HeightMm = 100m, VolumeMm3 = volume };
        var landed = detected with { Key = "landed-" + id, RecordId = "landed-" + id, Stage = ParcelProcessingStage.SortingCompleted,
            OccurredAt = at.AddMilliseconds(100), TargetChuteCode = "0013", ActualChuteCode = "13" };
        var assigned = detected with { Key = "assigned-" + id, RecordId = "assigned-" + id, Stage = ParcelProcessingStage.ChuteAssigned,
            OccurredAt = at.AddMilliseconds(50), TargetChuteCode = "0013" };
        var parcel = Parcel.CreateDetected(id, detected, at);
        parcel.ApplyProcessingRecords([detected, dws, assigned, landed]);
        parcel.AddApiRequest(new ApiRequestInfo { ApiType = ApiRequestType.RequestChute, RequestStatus = ApiRequestStatus.Success,
            RequestTime = at.AddMilliseconds(50), ResponseTime = at.AddMilliseconds(57).AddTicks(1), ElapsedMilliseconds = 999,
            RequestUrl = "https://internal.invalid/chute", RequestBody = "hidden", ResponseBody = "hidden" });
        Assert.True((await database.Parcels.AddAsync(parcel, default)).IsSuccess);
        await using var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default);
        db.AddRange(detected, dws, assigned, landed, detected with { Key = "foreign-" + id, RecordId = "foreign", SourceInstanceId = "other-source" });
        await db.SaveChangesAsync();
    }
}
