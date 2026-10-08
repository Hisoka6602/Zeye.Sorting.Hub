using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>真实关系数据库上的DWS重复测量、缺失数据、阈值、缓存与接口回归。</summary>
public sealed class ParcelDwsConsistencyTests {
    /// <summary>四种生产提供器都可翻译量测时间、阶段与来源过滤，不读取宽报文。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void NarrowMeasurementProjectionTranslatesForEveryProvider(string provider) {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(settings).AddSortingHubPersistence(settings)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ParcelDwsMeasurementSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""], streaming: true);
        var from = new DateTime(2026, 10, 1);
        var sql = read.Query(["202610"], nameof(ParcelProcessingRecord.PartitionTime), from, from.AddDays(1), false)
            .Where(row => (row.Stage == ParcelProcessingStage.DwsReceived || row.Stage == ParcelProcessingStage.DwsBound) && row.IsSuccess != false)
            .Where(row => row.Stage != ParcelProcessingStage.DwsBound || row.IsSuccess == true)
            .Where(row => row.SourceInstanceId == "source-a").ToQueryString();
        Assert.DoesNotContain("UNION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawPayload", sql, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("ErrorMessage", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(ParcelProcessingRecord.MessageIdentity), sql); Assert.Contains(nameof(ParcelProcessingRecord.PartitionTime), sql);
        Assert.Contains(nameof(ParcelProcessingRecord.ReceivedAt), sql); Assert.Contains(nameof(ParcelProcessingRecord.HasReliableTimestamp), sql);
        Assert.False(read.Database.CreateExecutionStrategy().RetriesOnFailure);
        using var identities = ParcelPartitionReadContext<ParcelDwsParcelIdentitySnapshot>.Create<Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Parcel>(db, ["202610", ""], streaming: true);
        var ids = Enumerable.Range(1, 500).Select(value => (long)value).ToArray();
        var identitySql = identities.Query(["202610"], "CreatedTime", from, from.AddDays(1), false).Where(parcel => ids.Contains(parcel.Id)).ToQueryString();
        Assert.Contains(" IN ", identitySql, StringComparison.OrdinalIgnoreCase); Assert.Contains("BarCodes", identitySql);
        Assert.DoesNotContain("ParcelInfos", identitySql, StringComparison.OrdinalIgnoreCase);
        using var detections = ParcelPartitionReadContext<ParcelDwsDetectionSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""], streaming: true);
        var detectionSql = detections.Query(["202610"], "PartitionTime", from, from.AddDays(1), false)
            .Where(row => row.Stage == ParcelProcessingStage.Detected && row.ParcelId != null && ids.Contains(row.ParcelId.Value)).ToQueryString();
        Assert.Contains("OccurredAt", detectionSql); Assert.Contains("SourceRunId", detectionSql);
        Assert.DoesNotContain("RawPayload", detectionSql, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("UNION", detectionSql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>接收与绑定只算一次，同条码跨票对比不合并包裹，来源隔离相同消息编号。</summary>
    [Fact]
    public async Task DedupeRetainsMultipleParcelsAndSourceIdentity() {
        await using var db = new RelationalParcelTestDatabase("PerDay"); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Detect(db, "a", 1, at); await Detect(db, "a", 2, at.AddSeconds(1)); await Detect(db, "b", 1, at.AddSeconds(2));
        await Save(db, Measurement("rx-1", "m-1", "a", at, 1000m));
        await Save(db, Measurement("bind-1", "m-1", "a", at, 1000m) with { Stage = ParcelProcessingStage.DwsBound, SourceParcelId = 1, FinalSourceParcelId = 1, IsSuccess = true, OccurredAt = at.AddDays(1) });
        await Save(db, Measurement("rx-2", "m-2", "a", at.AddSeconds(1), 1020m) with { SourceParcelId = 2 });
        await Save(db, Measurement("rx-b", "m-1", "b", at.AddSeconds(2), 1100m) with { SourceParcelId = 1, VolumeMm3 = 2200000m });
        var result = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SAME" }, default);
        Assert.Equal(3, result.MeasurementCount); Assert.Equal(1, result.DuplicateRecordCount);
        var group = Assert.Single(result.Items);
        Assert.Equal(3, group.MeasurementCount); Assert.Equal(2, group.SourceCount); Assert.Equal(3, group.ParcelCount);
        Assert.Equal(1020m, group.Weight.Median); Assert.Equal(100m, group.Weight.Spread); Assert.True(group.WeightDeviates);
        Assert.Equal(2000m, group.Volume.Minimum); Assert.Equal(2200m, group.Volume.Maximum);
        Assert.Equal(3, result.Detail!.Items.Select(sample => sample.Key).Distinct().Count());
        Assert.True(result.Detail.Items.Single(sample => sample.MessageIdentity == "m-1" && sample.SourceInstanceId == "a").BindingConfirmed);
        Assert.Equal(2, result.Sources.Count); Assert.Equal(0m, result.Sources.Single(source => source.SourceInstanceId == "a").MedianVolumeDeviationPercent);
    }

    /// <summary>缺失身份、冲突身份和未读条码明确排除，真实零重量有效，缺失体积不补零。</summary>
    [Fact]
    public async Task UnknownAndConflictingIdentityNeverFabricateRepeatedSamples() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Save(db, Measurement("missing", "", "a", at, 1000m));
        await Save(db, Measurement("conflict-1", "collision", "a", at, 1000m));
        await Save(db, Measurement("conflict-2", "collision", "a", at, 1300m));
        await Save(db, Measurement("noread", "m-noread", "a", at, 1000m) with { Barcode = "NoRead" });
        await Save(db, Measurement("zero-1", "m-zero-1", "a", at, 0m) with { Barcode = "ZERO", VolumeMm3 = null, LengthMm = null });
        await Save(db, Measurement("zero-2", "m-zero-2", "a", at, 0m) with { Barcode = "ZERO", VolumeMm3 = null, LengthMm = null, MeasuredAt = null });
        await Save(db, Measurement("failed", "m-failed", "a", at, 500m) with { IsSuccess = false });
        var result = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "ZERO" }, default);
        Assert.Equal(2, result.MeasurementCount); Assert.Equal(1, result.MissingIdentityCount);
        Assert.Equal(1, result.ConflictingMeasurementCount); Assert.Equal(1, result.MissingBarcodeCount);
        var zero = Assert.Single(result.Items);
        Assert.Equal(0m, zero.Weight.Median); Assert.Equal(0m, zero.Weight.Spread); Assert.Null(zero.Weight.SpreadPercent);
        Assert.Equal(0, zero.Volume.Count); Assert.Null(zero.Volume.Median); Assert.False(zero.WeightDeviates);
        Assert.Single(result.Detail!.Trend); Assert.Equal(2, result.Detail.Items.Count);
        Assert.All(result.Detail.Items, sample => Assert.Null(sample.ParcelId));
    }

    /// <summary>条码大小写和来源会话不折叠；排序、阈值切换复用快照，显式刷新读取新增量测。</summary>
    [Fact]
    public async Task ThresholdsReferencesAndRefreshKeepTheWholeCohort() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        using var cache = new ParcelDwsConsistencyCache(); var reader = Reader(db, cache);
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Save(db, Measurement("first", "same-id", "a", at, 1000m));
        await Save(db, Measurement("second", "same-id", "a", at.AddSeconds(1), 1020m) with { SourceRunId = "run-2" });
        await Save(db, Measurement("lowercase", "third", "a", at, 1200m) with { Barcode = "same" });
        var request = Request(at) with { DetailBarcode = "SAME" };
        var first = await reader.ReadAsync(request, default);
        Assert.Equal(3, first.MeasurementCount); Assert.False(Assert.Single(first.Items).WeightDeviates);
        var adjusted = await reader.ReadAsync(request with { WeightToleranceGrams = 19m, WeightTolerancePercent = 1m }, default);
        Assert.True(Assert.Single(adjusted.Items).WeightDeviates); Assert.Equal(first.GeneratedAt, adjusted.GeneratedAt);
        var relative = await reader.ReadAsync(request with { WeightToleranceGrams = 19m, WeightTolerancePercent = 2m, OnlyDeviations = true }, default);
        Assert.Empty(relative.Items); Assert.Equal(3, relative.MeasurementCount);
        var reference = await reader.ReadAsync(request with { Barcode = "same", DetailBarcode = "same", ReferenceWeightGrams = 1000m }, default);
        Assert.True(Assert.Single(reference.Items).ReferenceDeviates); Assert.Equal(200m, reference.Detail!.Summary.Weight.MaximumReferenceDeviation);
        await Save(db, Measurement("new", "fourth", "a", at, 1500m));
        Assert.Equal(3, (await reader.ReadAsync(request, default)).MeasurementCount);
        Assert.Equal(4, (await reader.ReadAsync(request with { Refresh = true }, default)).MeasurementCount);
    }

    /// <summary>明细分页不改变统计总体，趋势限量使用真实首尾时间，不伪造聚合点。</summary>
    [Fact]
    public async Task DetailPaginationAndTrendSamplingPreserveActualEndpoints() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        for (var index = 0; index < 205; index++) await Save(db, Measurement("r-" + index, "m-" + index, "a", at.AddSeconds(index), 1000m + index));
        var report = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SAME", MeasurementPageNumber = 11 }, default);
        Assert.Equal(205, report.MeasurementCount); Assert.Equal(205, report.Detail!.MeasurementCount);
        Assert.Equal(5, report.Detail.Items.Count); Assert.True(report.Detail.TrendTruncated); Assert.Equal(200, report.Detail.Trend.Count);
        Assert.Equal(at, report.Detail.Trend[0].MeasuredAt); Assert.Equal(at.AddSeconds(204), report.Detail.Trend[^1].MeasuredAt);
        Assert.Equal(1102m, report.Detail.Summary.Weight.Median);
    }

    /// <summary>完整尺寸可以推导物理体积，体积重量不能代替缺失体积；重量和体积独立形成有效样本。</summary>
    [Fact]
    public async Task PhysicalVolumeAndWeightUseIndependentValidSamples() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Save(db, Measurement("dims-1", "m-1", "a", at, 1000m) with { VolumeMm3 = null });
        await Save(db, Measurement("dims-2", "m-2", "a", at.AddSeconds(1), 1000m) with { VolumeMm3 = null, WeightGrams = null, HeightMm = 120m });
        await Save(db, Measurement("missing-volume", "m-3", "a", at.AddSeconds(2), 1000m) with { VolumeMm3 = null, HeightMm = null });
        await Save(db, Measurement("unknown-binding", "m-4", "a", at.AddSeconds(3), 1500m) with { Stage = ParcelProcessingStage.DwsBound, IsSuccess = null });
        var group = Assert.Single((await Reader(db).ReadAsync(Request(at), default)).Items);
        Assert.Equal(3, group.MeasurementCount); Assert.Equal(2, group.Weight.Count); Assert.Equal(2, group.Volume.Count);
        Assert.Equal(0m, group.Weight.Spread); Assert.Equal(2200m, group.Volume.Median); Assert.Equal(400m, group.Volume.Spread);
        Assert.True(group.VolumeDeviates); Assert.False(group.WeightDeviates);
    }

    /// <summary>缺少量测条码时只按已关联包裹编号与完整来源校验条码，同条码两票仍保持身份。</summary>
    [Fact]
    public async Task MissingBarcodeUsesOnlyConfirmedParcelIdentity() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Detect(db, "a", 1, at); await Detect(db, "a", 2, at.AddSeconds(1));
        for (var index = 1; index <= 2; index++) await Save(db, Measurement("bound-" + index, "m-" + index, "a", at, 1000m + index)
            with { Stage = ParcelProcessingStage.DwsBound, SourceParcelId = index, FinalSourceParcelId = index, IsSuccess = true, Barcode = null });
        var report = await Reader(db).ReadAsync(Request(at) with { DetailBarcode = "SAME" }, default);
        Assert.Equal(2, report.MeasurementCount); Assert.Equal(0, report.MissingBarcodeCount);
        Assert.Equal(2, Assert.Single(report.Items).ParcelCount);
        Assert.Equal(2, report.Detail!.Items.Select(row => row.ParcelId).Distinct().Count());
    }

    /// <summary>单票条码不进入重复排行，仍计入来源总体，显式详情及标准参考值不能被优化省略。</summary>
    [Fact]
    public async Task UniqueBarcodeStillCountsInSourceAndKeepsExplicitDetailAndReference() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var at = new DateTime(2026, 10, 1, 10, 0, 0);
        await Save(db, Measurement("single", "single", "a", at, 1500m) with { Barcode = "SINGLE" });
        await Save(db, Measurement("repeat-a", "repeat-a", "a", at, 1000m));
        await Save(db, Measurement("repeat-b", "repeat-b", "b", at, 1100m));
        var reader = Reader(db);
        var result = await reader.ReadAsync(Request(at) with { DetailBarcode = "SINGLE" }, default);
        Assert.Equal(3, result.MeasurementCount);
        Assert.Equal(1, result.RepeatedBarcodeCount);
        Assert.Equal("SAME", Assert.Single(result.Items).Barcode);
        var source = Assert.Single(result.Sources, row => row.SourceInstanceId == "a");
        Assert.Equal(2, source.MeasurementCount);
        Assert.Equal(0, source.RepeatedBarcodeCount);
        Assert.NotNull(source.MedianWeightDeviationPercent);
        Assert.Equal(1500m, result.Detail!.Summary.Weight.Median);
        Assert.Null(result.Detail.Summary.Weight.Spread);
        var reference = await reader.ReadAsync(Request(at) with { Barcode = "SINGLE", ReferenceWeightGrams = 1000m }, default);
        Assert.Equal(1, reference.MeasurementCount);
        Assert.True(Assert.Single(reference.Items).ReferenceDeviates);
        Assert.Equal(500m, reference.Items[0].Weight.MaximumReferenceDeviation);
        Assert.Equal(1, Assert.Single(reference.Sources).MeasurementCount);
    }

    /// <summary>非法日期、负阈值与未指定条码的参考值在接口层拒绝，空结果保留未知。</summary>
    [Fact]
    public async Task HttpValidatesScopeAndDocumentsTheBusinessEndpoint() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(db.Factory); builder.Services.AddSingleton(db.Partitions);
        builder.Services.AddSingleton(new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())));
        builder.Services.AddScoped<IParcelDwsConsistencyReadService, ParcelDwsConsistencyReadService>();
        await using var app = builder.Build(); app.MapParcelDwsConsistencyApis(); await app.StartAsync();
        using var client = app.GetTestClient();
        const string path = "/api/parcels/dws-consistency?fromDate=2026-10-01&toDate=2026-10-01";
        foreach (var invalid in new[] { "/api/parcels/dws-consistency", path + "&sortBy=wrong", path + "&pageNumber=0",
            path + "&measurementPageNumber=-1", path + "&weightToleranceGrams=-1", path + "&referenceWeightGrams=1000",
            path + "&barcode=SAME&referenceVolumeCm3=0", path + "&weightTolerancePercent=NaN",
            path + "&scanDurationToleranceMilliseconds=-1", path + "&scanDurationTolerancePercent=NaN",
            "/api/parcels/dws-consistency?fromDate=2026-10-01Z&toDate=2026-10-01",
            "/api/parcels/dws-consistency?fromDate=2026-10-01&toDate=2026-11-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(invalid)).StatusCode);
        var report = await client.GetFromJsonAsync<ParcelDwsConsistencyResponse>(path);
        Assert.NotNull(report); Assert.Equal(0, report.MeasurementCount); Assert.Empty(report.Items); Assert.Null(report.Detail);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "&sortBy=scan-duration-p95&scanDurationToleranceMilliseconds=0")).StatusCode);
        var endpoint = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Single(item => item.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName == "GetParcelDwsConsistency");
        Assert.Contains("DWS", endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()!.Description);
        Assert.Contains("扫码耗时", endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IEndpointDescriptionMetadata>()!.Description);
    }

    /// <summary>生产同款预算与分表窄查询。</summary>
    private static ParcelDwsConsistencyReadService Reader(RelationalParcelTestDatabase db, ParcelDwsConsistencyCache? cache = null) => new(
        db.Factory, new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), db.Partitions, cache);
    /// <summary>单日请求。</summary>
    private static ParcelDwsConsistencyRequest Request(DateTime at) => new() { FromDate = at.Date, ToDate = at.Date };
    /// <summary>来自DWS的明确测量身份与物理尺寸。</summary>
    private static ParcelProcessingRecord Measurement(string id, string message, string source, DateTime at, decimal weight) => new() {
        RecordId = id, PayloadHash = id, MessageIdentity = message, SourceInstanceId = source, SourceRunId = "run-1", WorkstationName = "工作台-" + source,
        Stage = ParcelProcessingStage.DwsReceived, PartitionTime = at, RecordedAt = at, OccurredAt = at, MeasuredAt = at,
        Barcode = "SAME", WeightGrams = weight, LengthMm = 200m, WidthMm = 100m, HeightMm = 100m, VolumeMm3 = 2000000m, VolumetricWeightGrams = 9999m
    };
    /// <summary>先建立独立来源包裹，DWS绑定沿用真实包裹身份。</summary>
    private static Task Detect(RelationalParcelTestDatabase db, string source, long parcel, DateTime at) => Save(db,
        Measurement("detect-" + source + parcel, "", source, at, 1000m) with { Stage = ParcelProcessingStage.Detected, SourceParcelId = parcel });
    /// <summary>写入测试专用事实，确认仓储成功。</summary>
    private static async Task Save(RelationalParcelTestDatabase db, ParcelProcessingRecord record) {
        var saved = await db.Processing.AppendAsync(record, default); Assert.True(saved.IsSuccess, saved.ErrorMessage);
    }
}
