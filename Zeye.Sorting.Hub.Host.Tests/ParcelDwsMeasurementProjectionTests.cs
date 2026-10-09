using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证耐久DWS读模型的原子保存、历史补齐、分表与原始事实口径一致性。</summary>
public sealed class ParcelDwsMeasurementProjectionTests {
    /// <summary>四种生产提供器的快照及补齐查询都保持窄列、有界主键继续和无LOB。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void EveryProviderTranslatesNarrowProjectionAndBoundedBackfill(string provider) {
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
        var factModel = db.Model.FindEntityType(typeof(ParcelProcessingRecord))!;
        var measurementModel = db.Model.FindEntityType(typeof(ParcelDwsMeasurementSnapshot))!;
        foreach (var property in measurementModel.GetProperties()) {
            var original = factModel.FindProperty(property.Name)!;
            Assert.Equal(original.GetMaxLength(), property.GetMaxLength());
            Assert.Equal(original.GetPrecision(), property.GetPrecision());
            Assert.Equal(original.GetScale(), property.GetScale());
        }
        var from = new DateTime(2026, 10, 6);
        using var read = ParcelPartitionReadContext<ParcelDwsMeasurementSnapshot>.Create<ParcelDwsMeasurementSnapshot>(db, ["202610", ""], streaming: true);
        var sql = read.Query(["202610"], nameof(ParcelDwsMeasurementSnapshot.PartitionTime), from, from.AddDays(1), false)
            .Where(row => row.Stage == ParcelProcessingStage.DwsReceived || row.Stage == ParcelProcessingStage.DwsBound).ToQueryString();
        Assert.Contains("Parcel_DwsMeasurements_202610", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Parcel_ProcessingRecords", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WeightGrams", sql); Assert.Contains("ReceivedAt", sql);
        Assert.DoesNotContain("RawPayload", sql); Assert.DoesNotContain("ErrorMessage", sql);
        var batch = ParcelDwsMeasurementBackfillService.BuildBatch(db.Set<ParcelProcessingRecord>().AsNoTracking(), "cursor").ToQueryString();
        Assert.Contains("ORDER BY", batch, StringComparison.OrdinalIgnoreCase); Assert.Contains("Key", batch);
        Assert.DoesNotContain("OFFSET", batch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawPayload", batch); Assert.DoesNotContain("RequestBody", batch);
    }

    /// <summary>新事实窄投影同事务保存，历史补齐幂等；补齐后即使原表不可供查询也保持完整业务结果。</summary>
    [Fact]
    public async Task DurableProjectionPreservesMeasurementsIdentityAndScanEndpoints() {
        await using var database = new RelationalParcelTestDatabase("PerDay"); await database.InitializeAsync();
        var at = new DateTime(2026, 10, 6, 10, 0, 0);
        foreach (var day in new[] { 0, 1 }) {
            var time = at.AddDays(day); var id = 9007199254741001L + day;
            await database.Partitions.EnsureCreatedAsync(database.Partitions.Resolve(time), default);
            await using var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(time).Suffix, default);
            var detected = Fact("detected-" + day, id, time) with { Stage = ParcelProcessingStage.Detected, MessageIdentity = null };
            var received = Fact("received-" + day, id, time.AddTicks(1001)) with { MessageIdentity = new string('m', 240) + day, ReceivedAt = time.AddTicks(1001), WeightGrams = day == 0 ? 0 : 1020 };
            var bound = received with { Key = "bound-" + day, RecordId = "bound-" + day, Stage = ParcelProcessingStage.DwsBound };
            db.AddRange(detected, received, bound);
            await db.SaveChangesAsync();
            Assert.Equal(3, await db.Set<ParcelDwsMeasurementSnapshot>().CountAsync());
            Assert.All(await db.Set<ParcelDwsMeasurementSnapshot>().ToListAsync(), row => Assert.True(row.Key.StartsWith("detected-", StringComparison.Ordinal) || row.Key.StartsWith("received-", StringComparison.Ordinal) || row.Key.StartsWith("bound-", StringComparison.Ordinal)));
            // 模拟升级前的历史记录：只清除测试数据库中的派生读模型，原事实完整保留。
            await db.Set<ParcelDwsMeasurementSnapshot>().ExecuteDeleteAsync();
        }
        var reader = new ParcelDwsConsistencyReadService(database.Factory,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), database.Partitions);
        var request = new ParcelDwsConsistencyRequest { FromDate = at.Date, ToDate = at.AddDays(1).Date, DetailBarcode = "SAME" };
        var before = await reader.ReadAsync(request, default);
        Assert.Equal(2, before.MeasurementCount); Assert.Equal(2, before.DuplicateRecordCount);
        Assert.Equal(2, before.ScanTimingSampleCount);
        Assert.All(before.Detail!.Items, sample => Assert.Equal(.1001m, sample.ScanDurationMilliseconds));
        var backfill = new ParcelDwsMeasurementBackfillService(database.Factory, database.Partitions);
        for (var attempt = 0; attempt < 12; attempt++) await backfill.RunBatchAsync(default);
        foreach (var period in (await database.Partitions.GetReadCatalogAsync(default)).Periods) {
            await using var db = await database.Partitions.CreateContextAsync(period.Suffix, default);
            Assert.Equal(3, await db.Set<ParcelDwsMeasurementSnapshot>().CountAsync());
            // 快照查询必须只依赖窄表；测试原表清空会暴露缓存映射或错误回退。
            await db.Set<ParcelProcessingRecord>().ExecuteDeleteAsync();
        }
        var after = await reader.ReadAsync(request, default);
        Assert.Equal(JsonSerializer.Serialize(before with { GeneratedAt = at }), JsonSerializer.Serialize(after with { GeneratedAt = at }));
        Assert.Equal(0, await backfill.RunBatchAsync(default));
    }

    /// <summary>补齐批次中断时未提交进度不能被标记完整，重复执行不重复插入新事实投影。</summary>
    [Fact]
    public async Task BackfillUsesBoundedCursorAndDoesNotDuplicateAlreadyProjectedRows() {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var at = new DateTime(2026, 10, 6, 10, 0, 0);
        await database.Partitions.EnsureCreatedAsync(database.Partitions.Resolve(at), default);
        await using (var db = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default)) {
            db.AddRange(Enumerable.Range(0, 600).Select(index => Fact("record-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), 42, at.AddMilliseconds(index))));
            await db.SaveChangesAsync();
        }
        var backfill = new ParcelDwsMeasurementBackfillService(database.Factory, database.Partitions);
        Assert.Equal(512, await backfill.RunBatchAsync(default));
        await using (var db = await database.Factory.CreateDbContextAsync()) Assert.False((await db.Set<ParcelDurationBackfillState>().SingleAsync()).DwsCompleted);
        Assert.Equal(88, await backfill.RunBatchAsync(default));
        for (var attempt = 0; attempt < 3; attempt++) await backfill.RunBatchAsync(default);
        await using var read = await database.Partitions.CreateContextAsync(database.Partitions.Resolve(at).Suffix, default);
        Assert.Equal(600, await read.Set<ParcelDwsMeasurementSnapshot>().CountAsync());
    }

    /// <summary>明确关联与真实接收时间的固定测试事实。</summary>
    private static ParcelProcessingRecord Fact(string key, long id, DateTime at) => new() {
        Key = key, RecordId = key, ParcelId = id, SourceParcelId = id, SourceInstanceId = "source-a", SourceRunId = "run-a",
        WorkstationName = "工作台A", MessageIdentity = "measurement-" + id, Barcode = "SAME",
        Stage = ParcelProcessingStage.DwsReceived, IsSuccess = true, HasReliableTimestamp = true,
        PartitionTime = at.Date, RecordedAt = at, OccurredAt = at, MeasuredAt = at.Date,
        WeightGrams = 1000, LengthMm = 200, WidthMm = 100, HeightMm = 100, VolumeMm3 = 2000000
    };
}
