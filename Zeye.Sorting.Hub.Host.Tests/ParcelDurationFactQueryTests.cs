using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证耗时事实按包裹索引读取、批次边界和完整统计总体。</summary>
public sealed class ParcelDurationFactQueryTests {
    /// <summary>显式索引的建表与迁移重定位使用同一物理名称，回滚不会删除基础表索引。</summary>
    [Theory]
    [InlineData("IX_Processing_Stage_PartitionTime_Duration")]
    [InlineData("IX_Processing_Source_Stage_Duration")]
    public async Task ExplicitIndexMigrationTargetsThePhysicalModelAndPreservesBaseIndex(string indexName) {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var baseline = await database.Factory.CreateDbContextAsync();
        var design = baseline.GetService<IDesignTimeModel>().Model;
        var source = baseline.GetService<IMigrationsModelDiffer>().GetDifferences(null, design.GetRelationalModel())
            .OfType<CreateIndexOperation>().Single(index => index.Name == indexName);
        await using var physical = await database.Partitions.CreateContextAsync("202610", default);
        var entity = physical.Model.FindEntityType(typeof(ParcelProcessingRecord))!;
        var expected = entity.GetIndexes().Single(index => index.Properties.Select(property => property.Name).SequenceEqual(source.Columns)).GetDatabaseName();
        var tables = new HashSet<string>(StringComparer.Ordinal) { source.Table };
        var create = (CreateIndexOperation)PartitionMigrationOperationRebaser.Rebase(source, "202610", tables, physical.Model.GetMaxIdentifierLength(), false);
        var drop = (DropIndexOperation)PartitionMigrationOperationRebaser.Rebase(new DropIndexOperation { Table = source.Table, Schema = source.Schema, Name = source.Name },
            "202610", tables, physical.Model.GetMaxIdentifierLength(), false);
        Assert.Equal(entity.GetTableName(), create.Table);
        Assert.Equal(expected, create.Name);
        Assert.Equal(create.Table, drop.Table);
        Assert.Equal(create.Name, drop.Name);
        Assert.NotEqual(source.Name, drop.Name);
        Assert.Equal("Parcel_ProcessingRecords", source.Table);
    }

    /// <summary>四种提供器可以翻译最大编号批次，时间、阶段与来源条件仍由数据库执行。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void IndexedCohortBatchTranslatesForSupportedProviders(string provider) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        // 一个测试进程同时验证四种提供器与两种执行策略，独立服务容器不进入EF全局生产缓存。
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""]);
        using var streaming = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""], streaming: true);
        Assert.Equal(db.Database.CreateExecutionStrategy().RetriesOnFailure, read.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.False(streaming.Database.CreateExecutionStrategy().RetriesOnFailure);
        var from = new DateTime(2026, 10, 1);
        var facts = read.Query(["202610"], nameof(ParcelProcessingRecord.PartitionTime), from, from.AddDays(1), false);
        var ids = Enumerable.Range(1, ParcelDurationAnalysisReader.FactBatchSize).Select(number => (long)number).ToArray();
        var sql = ParcelDurationAnalysisReader.BuildFactProjection(facts, [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound], false, "source-a", ids).ToQueryString();
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" IN ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[.](?:`|\\\"|\\[)?(?:RawPayload|ErrorMessage)(?:`|\\\"|\\])?", sql);
        Assert.DoesNotContain(nameof(ParcelDurationFactSnapshot.RawPayload), sql);
        Assert.DoesNotContain(nameof(ParcelDurationFactSnapshot.Provider), sql);
        Assert.Contains(nameof(ParcelProcessingRecord.PartitionTime), sql, StringComparison.Ordinal);
        Assert.Contains(nameof(ParcelProcessingRecord.SourceInstanceId), sql, StringComparison.Ordinal);
    }

    /// <summary>查询计划搜索索引而不扫描事实表，排除不属于所选总体的大批宽报文。</summary>
    [Fact]
    public async Task NarrowStageQueryUsesIndexAndKeepsLateEndpoints() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 1);
        var rows = Enumerable.Range(1, 3000).Select(number => Fact(number, "detected-" + number, from) with {
            RawPayload = new string('x', 4096), RequestBody = new string('y', 4096)
        }).ToList();
        rows.Add(Fact(42, "late-dws", from) with { Stage = ParcelProcessingStage.DwsBound, OccurredAt = from.AddDays(2) });
        rows.Add(Fact(42, "end-boundary", from.AddDays(1)) with { Stage = ParcelProcessingStage.DwsBound });
        await using var db = await database.Factory.CreateDbContextAsync();
        db.AddRange(rows);
        await db.SaveChangesAsync();
        using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, [""]);
        var facts = read.Query([""], nameof(ParcelProcessingRecord.PartitionTime), from, from.AddDays(1), false);
        capture.Commands.Clear();
        var result = await ParcelDurationAnalysisReader.BuildFactProjection(facts,
            [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound], false, null, [42]).ToArrayAsync();
        Assert.Equal(new[] { "detected-42", "late-dws" }, result.Select(row => row.RecordId).Order(StringComparer.Ordinal));
        Assert.All(result, row => Assert.Null(row.RawPayload));
        var captured = Assert.Single(capture.Commands);
        await db.Database.OpenConnectionAsync();
        await using var plan = db.Database.GetDbConnection().CreateCommand();
        plan.CommandText = "EXPLAIN QUERY PLAN " + captured.Sql;
        foreach (var (name, value, type) in captured.Parameters) {
            var parameter = plan.CreateParameter();
            parameter.ParameterName = name; parameter.Value = value; parameter.DbType = type;
            plan.Parameters.Add(parameter);
        }
        var details = new List<string>();
        await using var reader = await plan.ExecuteReaderAsync();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.Contains(details, detail => detail.StartsWith("SEARCH p USING", StringComparison.Ordinal)
            && detail.Contains("INDEX", StringComparison.Ordinal));
        Assert.DoesNotContain(details, detail => detail == "SCAN p" || detail.StartsWith("SCAN p ", StringComparison.Ordinal));
        Assert.DoesNotContain(details, detail => detail.Contains("TEMP B-TREE", StringComparison.Ordinal));
    }

    /// <summary>阶段分别沿等值索引读取，历史基础表与跨日端点保留；工作台外的身份不进入统计。</summary>
    [Fact]
    public async Task ReportReadsLargeCohortOncePerPartitionAndKeepsIdentityScope() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerDay", capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 1, 12, 0, 0);
        var period = database.Partitions.Resolve(from);
        await database.Partitions.EnsureCreatedAsync(period, default);
        var selected = ParcelDurationAnalysisReader.FactBatchSize + 5;
        await SeedCohortAsync(database, period.Suffix, from, selected, 100);
        await SeedCohortAsync(database, "", from, 1, 0, 10000);
        var request = new ParcelAnalysisRequest { View = "duration", DurationType = "dws", FromDate = from.Date, ToDate = from.Date, WorkstationName = "selected", SourceInstanceId = "source-a" };
        var service = new ParcelAnalysisReadService(database.Factory,
            new ReportingQueryBudgetPlanner(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions())), database.Partitions);
        capture.Commands.Clear();
        var response = await service.ReadAsync(request, default);
        var report = Assert.IsType<ParcelDurationAnalysisResponse>(response.DurationAnalysis);
        Assert.Equal(selected + 1, report.SampleCount);
        Assert.Equal(selected + 1, report.ObservedCount);
        Assert.Equal(0, report.UnavailableCount);
        Assert.Equal(1000m, report.MinimumMilliseconds);
        Assert.Equal(86400000m, report.MaximumMilliseconds);
        Assert.Equal(1000m, report.MedianMilliseconds);
        Assert.Equal(20, report.Items.Count);
        Assert.InRange(capture.Commands.Count, 1, 8);
        Assert.DoesNotContain(capture.Commands, command => command.Sql.Contains("UNION ALL", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capture.Commands, command => command.Sql.Contains(" IN ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(capture.Commands, command => command.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
        // 两个单时间值边界探测和两次事实读取，不以每包裹批量查询放大命令数。
        foreach (var command in capture.Commands.Where(command => !command.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase))) {
            Assert.DoesNotMatch("[.](?:`|\\\"|\\[)?(?:RawPayload|ErrorMessage)(?:`|\\\"|\\])?", command.Sql);
            Assert.DoesNotContain(command.Parameters, parameter => parameter.Name.Contains("parcelIds", StringComparison.Ordinal));
        }
        capture.Commands.Clear();
        Assert.Empty((await service.ReadAsync(request with { WorkstationName = "missing" }, default)).DurationAnalysis!.Items);
        Assert.Empty(capture.Commands);
    }

    /// <summary>构造首次入库锚点与来源身份明确的处理事实。</summary>
    private static ParcelProcessingRecord Fact(long id, string key, DateTime at) => new() {
        Key = key, RecordId = key, ParcelId = id, SourceParcelId = id, SourceInstanceId = "source-a", SourceRunId = "run-a",
        Stage = ParcelProcessingStage.Detected, OccurredAt = at, PartitionTime = at, RecordedAt = at,
        Barcode = "PKG-" + id.ToString(CultureInfo.InvariantCulture), WorkstationName = "selected", IsSuccess = true, HasReliableTimestamp = true
    };

    /// <summary>直接在隔离分表写入领域聚合和原始事实，包含工作台外样本与错误来源会话。</summary>
    private static async Task SeedCohortAsync(RelationalParcelTestDatabase database, string suffix, DateTime at, int selected, int excluded, long offset = 0) {
        var parcels = new List<Parcel>();
        var records = new List<ParcelProcessingRecord>();
        for (var index = 1; index <= selected + excluded; index++) {
            var id = offset + index;
            var first = Fact(id, "detected-" + id.ToString(CultureInfo.InvariantCulture), at) with { WorkstationName = index <= selected ? "selected" : "excluded" };
            var parcel = Parcel.CreateDetected(id, first, at);
            parcel.ApplyProcessingRecords([first]);
            parcels.Add(parcel);
            records.Add(first);
            records.Add(first with { Key = "dws-" + first.Key, RecordId = "dws-" + first.RecordId, Stage = ParcelProcessingStage.DwsBound,
                OccurredAt = index == selected ? at.AddDays(1) : at.AddSeconds(1), FinalSourceParcelId = id });
        }
        var wrong = records[0] with { Key = "foreign-" + suffix, RecordId = "foreign-" + suffix, Stage = ParcelProcessingStage.DwsBound,
            SourceRunId = "other-run", OccurredAt = at.AddMilliseconds(1) };
        records.Add(wrong);
        await using var db = await database.Partitions.CreateContextAsync(suffix, default);
        db.AddRange(parcels);
        db.AddRange(records);
        await db.SaveChangesAsync();
    }
}
