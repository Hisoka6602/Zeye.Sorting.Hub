using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>分段读取必须保持半开边界、毫秒内密集事实和完整统计总体。</summary>
public sealed class ParcelAnalysisWindowReaderTests {
    /// <summary>索引边界不截断密集时间点，不纳入区间外或错误来源的事实。</summary>
    [Fact]
    public async Task SplitWindowsKeepEveryFactIncludingDenseSubMillisecondEndpoints() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 8, 12, 0, 0);
        var expected = Enumerable.Range(0, 32).Select(index => Fact("fact-" + index, from.AddTicks(index * 2500))).ToList();
        expected.AddRange(Enumerable.Range(0, 12).Select(index => Fact("dense-" + index, from.AddTicks(5000))));
        await using var db = await database.Factory.CreateDbContextAsync();
        db.AddRange(expected);
        db.AddRange(Fact("before", from.AddTicks(-1)), Fact("end", from.AddMilliseconds(8)),
            Fact("other-source", from) with { SourceInstanceId = "other-source" });
        await db.SaveChangesAsync();
        using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, [""], streaming: true);
        var facts = read.Query([""], nameof(ParcelProcessingRecord.PartitionTime), from, from.AddMilliseconds(8), false);
        IQueryable<ParcelDurationFactSnapshot> QueryWindow(DateTime start, DateTime end) => ParcelDurationAnalysisReader.BuildFactProjection(
            ParcelAnalysisWindowReader.Window(facts, start, end), [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound], false, "source-a", null);
        capture.Commands.Clear();
        var actual = new List<ParcelDurationFactSnapshot>();
        await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(QueryWindow, from, from.AddMilliseconds(8), default, targetRows: 4))
        await foreach (var row in window.AsAsyncEnumerable()) actual.Add(row);
        Assert.Equal(expected.Select(row => row.RecordId).Order(), actual.Select(row => row.RecordId).Order());
        Assert.Equal(actual.Count, actual.Select(row => row.RecordId).Distinct().Count());
        Assert.DoesNotContain(capture.Commands, command => command.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
        Assert.True(capture.Commands.Count <= 16);
        var commands = capture.Commands.Where(command => !command.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(commands.Length > 1);
        Assert.All(commands, command => Assert.DoesNotMatch("[.](?:`|\\\"|\\[)?(?:RawPayload|ErrorMessage)(?:`|\\\"|\\])?", command.Sql));
    }

    /// <summary>调用方取消后不得继续打开下一个读取器。</summary>
    [Fact]
    public async Task CancellationStopsBeforeOpeningAQuery() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        await using var db = await database.Factory.CreateDbContextAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var from = new DateTime(2026, 10, 8);
        capture.Commands.Clear();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (var row in ParcelAnalysisWindowReader.ReadAsync(db.Set<ParcelProcessingRecord>(), from, from.AddDays(1), cancellation.Token)) { }
        });
        Assert.Empty(capture.Commands);
    }

    /// <summary>非法范围或行数预算不执行数据库命令。</summary>
    [Fact]
    public async Task InvalidWindowDoesNotOpenAQuery() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using var db = await database.Factory.CreateDbContextAsync();
        var from = new DateTime(2026, 10, 8);
        foreach (var (to, targetRows) in new[] { (from, 4), (from.AddTicks(-1), 4), (from.AddDays(1), 0), (from.AddDays(1), int.MaxValue) }) {
            await Assert.ThrowsAsync<ArgumentException>(async () => {
                await foreach (var row in ParcelAnalysisWindowReader.ReadAsync(db.Set<ParcelProcessingRecord>(), from, to, default, targetRows)) { }
            });
        }
    }

    /// <summary>四种 EF 提供器均在数据库内定位时间边界并筛选，不加载宽报文。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void WindowTranslatesForEverySupportedProvider(string provider) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration)
            .AddDbContextFactory<SortingHubDbContext>(options => options.EnableServiceProviderCaching(false)).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ParcelDurationFactSnapshot>.Create<ParcelProcessingRecord>(db, ["202610", ""], streaming: true);
        var from = new DateTime(2026, 10, 8);
        var facts = read.Query(["202610"], nameof(ParcelProcessingRecord.PartitionTime), from, from.AddDays(1), false);
        var window = ParcelAnalysisWindowReader.Window(facts, from.AddMilliseconds(1), from.AddMilliseconds(2));
        var sql = ParcelDurationAnalysisReader.BuildFactProjection(window,
            [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound], false, "source-a", null).ToQueryString();
        Assert.Contains(nameof(ParcelProcessingRecord.PartitionTime), sql);
        Assert.Contains(nameof(ParcelProcessingRecord.SourceInstanceId), sql);
        Assert.DoesNotMatch("[.](?:`|\\\"|\\[)?(?:RawPayload|ErrorMessage)(?:`|\\\"|\\])?", sql);
        Assert.DoesNotContain(nameof(ParcelDurationFactSnapshot.RawPayload), sql);
        Assert.DoesNotContain(nameof(ParcelDurationFactSnapshot.Provider), sql);
        var boundarySql = ParcelAnalysisWindowReader.BoundaryQuery(ParcelDurationAnalysisReader.BuildFactProjection(window,
            [ParcelProcessingStage.Detected, ParcelProcessingStage.DwsBound], false, "source-a", null), 4).ToQueryString();
        Assert.Contains("ORDER BY", boundarySql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(", boundarySql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(ParcelDurationFactSnapshot.RawPayload), boundarySql);
    }

    /// <summary>长时间空档不触发递归探测，第一日和最后一日的样本仍完整读取。</summary>
    [Fact]
    public async Task SparseRangeUsesOneBoundaryProbePerWindowAndKeepsTheLastDay() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 1);
        var facts = Enumerable.Range(0, 9).Select(index => Fact("early-" + index, from.AddMilliseconds(index)))
            .Append(Fact("last-day", from.AddDays(6).AddHours(23))).ToArray();
        await using var db = await database.Factory.CreateDbContextAsync();
        db.AddRange(facts);
        await db.SaveChangesAsync();
        capture.Commands.Clear();
        var actual = new List<ParcelProcessingRecord>();
        await foreach (var window in ParcelAnalysisWindowReader.ReadAsync(db.Set<ParcelProcessingRecord>().AsNoTracking(), from, from.AddDays(7), default, 3))
        await foreach (var row in window.AsAsyncEnumerable()) actual.Add(row);
        Assert.Equal(facts.Select(row => row.Key).Order(), actual.Select(row => row.Key).Order());
        Assert.Equal(8, capture.Commands.Count);
        Assert.DoesNotContain(capture.Commands, command => command.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>构造来源身份和入库分表时间明确的事实。</summary>
    private static ParcelProcessingRecord Fact(string id, DateTime at) => new() {
        Key = id, RecordId = id, ParcelId = 1, SourceParcelId = 1, SourceInstanceId = "source-a", SourceRunId = "run-a",
        Stage = ParcelProcessingStage.Detected, OccurredAt = at, PartitionTime = at, RecordedAt = at,
        IsSuccess = true, HasReliableTimestamp = true
    };
}
