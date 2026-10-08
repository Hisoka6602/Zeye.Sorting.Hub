using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>通过真实物理表验证短窗口分页、跨周期晚到记录和投影队列的有界读取。</summary>
public sealed class SlowQueryReadPathTests {
    /// <summary>扫码日期与部分记录的分表锚点故意不同，禁止按扫码日期丢弃旧分表。</summary>
    private static readonly DateTime ScannedAt = new(2026, 10, 6, 10, 0, 0);

    /// <summary>四种驱动都能翻译同一有界队列查询；仅生成 SQL，不连接或初始化业务数据库。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void ProjectionCandidateQueryTranslatesForSupportedProviders(string provider) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSortingHubPersistence(configuration).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        var sql = FusionIngestionService.BuildProjectionCandidateQuery(db, ScannedAt).ToQueryString();
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BodyJson", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectionJson", sql, StringComparison.Ordinal);
    }

    /// <summary>短窗口和条码过滤同样使用分表索引，总数与分页、游标保持稳定。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShortWindowPagesAndCursorsReadBoundedPartitionsWithoutWideUnion(bool filterBarcode) {
        var capture = new SlowQueryReadCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerDay", capture);
        await database.InitializeAsync();
        await SeedParcelsAsync(database);
        capture.Commands.Clear();
        var filter = Filter(filterBarcode);
        var page = await database.Parcels.GetPagedAsync(filter, new() { PageNumber = 2, PageSize = 5 }, default);
        Assert.Equal(17, page.TotalCount);
        Assert.Equal(new long[] { 12, 11, 10, 9, 8 }, page.Items.Select(row => row.Id));
        var first = await database.Parcels.GetCursorPagedAsync(filter, new() { PageSize = 5 }, default);
        Assert.True(first.HasMore);
        var second = await database.Parcels.GetCursorPagedAsync(filter, new() {
            PageSize = 5, LastScannedTimeLocal = first.NextScannedTimeLocal, LastId = first.NextId
        }, default);
        Assert.Equal(page.Items.Select(row => row.Id), second.Items.Select(row => row.Id));
        Assert.NotEmpty(capture.Commands);
        Assert.All(capture.Commands, command => Assert.DoesNotContain("UNION", command.Sql, StringComparison.OrdinalIgnoreCase));
        Assert.All(capture.Commands.Where(command => !command.Sql.Contains("COUNT", StringComparison.OrdinalIgnoreCase)),
            command => Assert.Contains("LIMIT", command.Sql, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>显式关闭并行、超过分表限制或深分页时仍保留原来的兼容查询语义。</summary>
    [Theory]
    [InlineData("disabled")]
    [InlineData("partition-limit")]
    [InlineData("deep-page")]
    public async Task ConfiguredFallbacksPreserveExactCountAndPage(string reason) {
        var capture = new SlowQueryReadCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerDay", capture);
        await database.InitializeAsync();
        await SeedParcelsAsync(database);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Sharding:ReadFanout:Enabled"] = reason == "disabled" ? "false" : "true",
            ["Persistence:Sharding:ReadFanout:MaxPartitions"] = reason == "partition-limit" ? "1" : "12"
        }).Build();
        var repository = new ParcelRepository(database.Factory, configuration, database.Partitions);
        capture.Commands.Clear();
        var page = await repository.GetPagedAsync(Filter(false), new() {
            PageSize = 5, PageNumber = reason == "deep-page" ? 401 : 2
        }, default);
        Assert.Equal(17, page.TotalCount);
        if (reason == "deep-page") Assert.Empty(page.Items);
        else Assert.Equal(new long[] { 12, 11, 10, 9, 8 }, page.Items.Select(row => row.Id));
        Assert.Contains(capture.Commands, command => command.Sql.Contains("UNION ALL", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>状态分支各取有界候选再全局排序；未到重试时间、有效租约和完成历史均不被认领。</summary>
    [Fact]
    public async Task ProjectionQueueMergesPendingAndRetryWithGlobalLimitAndLeaseProtection() {
        var capture = new SlowQueryReadCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var now = DateTime.Now;
        var rows = Enumerable.Range(1, 1300).Select(number => QueueRow(number, now)).ToArray();
        rows[0].NextProjectionAt = now.AddHours(1);
        rows[1].ProjectionClaimId = "active";
        rows[1].ProjectionClaimUntil = now.AddHours(1);
        rows[2].ProjectionClaimId = "expired";
        rows[2].ProjectionClaimUntil = now.AddMinutes(-1);
        foreach (var row in rows.Skip(1200)) row.ProjectionState = "complete";
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.AddRange(rows);
            await db.SaveChangesAsync();
        }
        var expected = rows.Take(1200).Skip(2).OrderBy(row => row.ReceivedAt).ThenBy(row => row.SourceSequence)
            .ThenBy(row => row.Key, StringComparer.Ordinal).Take(512).Select(row => row.Key).ToArray();
        var ingress = new FusionIngestionService(database.Factory,
            Microsoft.Extensions.Options.Options.Create(new FusionIngestionOptions { ImageDirectory = "images" }), Path.GetTempPath());
        capture.Commands.Clear();
        var first = await ingress.ClaimProjectionsAsync(default);
        Assert.Equal(expected, first.Select(item => item.Key));
        var candidateSql = capture.Commands.First().Sql;
        Assert.Contains("UNION ALL", candidateSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending", candidateSql, StringComparison.Ordinal);
        Assert.Contains("retry", candidateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("BodyJson", candidateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectionJson", candidateSql, StringComparison.Ordinal);
        var second = await ingress.ClaimProjectionsAsync(default);
        Assert.Equal(512, second.Count);
        Assert.Empty(first.Select(item => item.Key).Intersect(second.Select(item => item.Key)));
        await using var verify = await database.Factory.CreateDbContextAsync();
        Assert.Equal(0, await verify.Set<FusionFactReceipt>().Where(row => row.ProjectionState == "complete").SumAsync(row => row.ProjectionAttempts));
        Assert.Equal(0, (await verify.Set<FusionFactReceipt>().SingleAsync(row => row.Key == rows[0].Key)).ProjectionAttempts);
        Assert.Equal("active", (await verify.Set<FusionFactReceipt>().SingleAsync(row => row.Key == rows[1].Key)).ProjectionClaimId);
    }

    /// <summary>建立包含条码子串的短扫码窗口。</summary>
    private static ParcelQueryFilter Filter(bool barcode) => new() {
        ScannedTimeStart = ScannedAt.AddHours(-1), ScannedTimeEnd = ScannedAt.AddHours(1),
        BarCodeKeyword = barcode ? "MATCH" : null
    };

    /// <summary>在基础表和两个物理周期保存相同扫码时间，验证全局主键排序及晚到旧周期数据。</summary>
    private static async Task SeedParcelsAsync(RelationalParcelTestDatabase database) {
        var createdTimes = new[] { ScannedAt, ScannedAt.AddDays(-1), ScannedAt.AddMonths(-1) };
        for (var group = 0; group < createdTimes.Length; group++) {
            var suffix = string.Empty;
            if (group > 0) {
                var period = database.Partitions.Resolve(createdTimes[group]);
                await database.Partitions.EnsureCreatedAsync(period, default);
                suffix = period.Suffix;
            }
            await using var db = await database.Partitions.CreateContextAsync(suffix, default);
            foreach (var number in Enumerable.Range(1, 18).Where(number => number % 3 == group)) {
                var fact = new ParcelProcessingRecord { RecordId = "query-" + number, PayloadHash = "query-" + number,
                    SourceInstanceId = "query-source", SourceRunId = "query-run", SourceParcelId = number,
                    Stage = ParcelProcessingStage.Detected, OccurredAt = number == 18 ? ScannedAt.AddDays(1) : ScannedAt,
                    RecordedAt = createdTimes[group], PartitionTime = createdTimes[group], Barcode = "PREFIX-MATCH-SUFFIX" };
                var entry = db.Add(Parcel.CreateDetected(number, fact, createdTimes[group]));
                entry.Property(parcel => parcel.BarCodes).CurrentValue = "PREFIX-MATCH-SUFFIX";
            }
            await db.SaveChangesAsync();
        }
    }

    /// <summary>构造两来源交错、相同接收时间与序号的队列，确保最后的主键顺序参与裁剪。</summary>
    private static FusionFactReceipt QueueRow(int number, DateTime now) => new() {
        Key = number.ToString("x64", CultureInfo.InvariantCulture), SourceInstanceId = "query-source-" + number % 2,
        JournalId = FusionIngressTestEnvironment.Journal, RecordId = number.ToString("x32", CultureInfo.InvariantCulture),
        SourceSequence = number / 2, ReceivedAt = now.AddHours(-1).AddSeconds(number / 4), OccurredAt = now.AddHours(-1),
        BodyJson = new string('x', 4096), BodySha256 = new string('a', 64), Kind = "parcel.detected",
        ProjectionState = number % 2 == 0 ? "pending" : "retry", NextProjectionAt = now.AddMinutes(-1),
        ProjectionJson = JsonSerializer.Serialize(new ParcelProcessingRecordRequest { RecordId = "queue-" + number,
            SourceInstanceId = "query-source", SourceRunId = "query-run", SourceParcelId = number,
            OccurredAt = now.AddHours(-1) }, FusionProtocol.Json)
    };
}
