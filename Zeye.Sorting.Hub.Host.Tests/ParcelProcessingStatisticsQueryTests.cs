using System.Data;
using System.Data.Common;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证处理质量统计的直接聚合、覆盖索引和跨周期事件口径。</summary>
public sealed class ParcelProcessingStatisticsQueryTests {
    /// <summary>四种提供器在每个物理表只生成一次统计读取，不使用独立标量计数或物化报文。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void DirectAggregationTranslatesForSupportedProviders(string provider) {
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
        using var processing = ParcelPartitionReadContext<ParcelProcessingStatisticsRow>.Create<ParcelProcessingRecord>(db, ["202611", "202609", ""]);
        var from = new DateTime(2026, 10, 8);
        var sql = ParcelProcessingStatisticsQuery.BuildQuery(processing, ["202611", "202609", ""], from, from.AddDays(1)).ToQueryString();
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(provider == "SqlServer" ? "COUNT_BIG(" : "COUNT(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, Regex.Count(sql, @"\bParcel_ProcessingRecords(?:_202611|_202609)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        Assert.DoesNotMatch(@"(?is)\bSELECT\s*\(\s*SELECT\b", sql);
        Assert.DoesNotContain("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(ParcelProcessingRecord.RawPayload), sql, StringComparison.Ordinal);
    }

    /// <summary>不同锚点的事件都按发生日计数；验证空窗口、半开边界、未知结果及未关联非 DWS。</summary>
    [Fact]
    public async Task CountsPreserveLateEventsNullResultsAndHalfOpenBoundaries() {
        var capture = new StatisticsCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerMonth", capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 8);
        foreach (var month in new[] { 9, 10, 11 })
            await database.Partitions.EnsureCreatedAsync(database.Partitions.Resolve(new DateTime(2026, month, 1)), default);
        await SaveAsync(database, "", Row("base", from, false, null, ParcelProcessingStage.DwsBound));
        await SaveAsync(database, "202609",
            Row("late-success", from.AddHours(1), true, 123, ParcelProcessingStage.ScanUploaded),
            Row("late-failed", from.AddHours(2), false, 123, ParcelProcessingStage.ScanUploaded),
            Row("before", from.AddTicks(-1), false, null, ParcelProcessingStage.DwsReceived),
            Row("end", from.AddDays(1), false, null, ParcelProcessingStage.DwsReceived));
        await SaveAsync(database, "202610",
            Row("unknown-dws", from.AddHours(3), null, null, ParcelProcessingStage.DwsReceived),
            Row("other-unbound", from.AddHours(4), false, null, ParcelProcessingStage.ScanUploaded));
        await SaveAsync(database, "202611", Row("future-anchor", from.AddHours(5), true, 123, ParcelProcessingStage.DwsBound));
        await using var db = await database.Factory.CreateDbContextAsync();
        var suffixes = await database.Partitions.GetReadSuffixesAsync(default);
        var totals = await ParcelProcessingStatisticsQuery.ReadAsync(db, suffixes, from, from.AddDays(1), default);
        Assert.Equal(6, totals.Count);
        Assert.Equal(3, totals.Failed);
        Assert.Equal(2, totals.UnboundDws);
        var empty = await ParcelProcessingStatisticsQuery.ReadAsync(db, suffixes, from.AddDays(2), from.AddDays(3), default);
        Assert.Equal(new ParcelProcessingStatisticsTotals(), empty);
        Assert.Equal(2, capture.Commands.Count);
        // 非空窗口和空窗口都必须每表只检索一次，不能用外层 LIMIT 隐藏重复计数。
        foreach (var command in capture.Commands) await AssertSingleCoveringRangePlanAsync(db, command, suffixes.Count);
    }

    /// <summary>大量事实每表只返回一行，统计包含可空状态且不加载报文明细。</summary>
    [Fact]
    public async Task LargeWindowReturnsBoundedStateGroupsRatherThanEvents() {
        var capture = new StatisticsCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase("PerMonth", capture);
        await database.InitializeAsync();
        var from = new DateTime(2026, 10, 8);
        await database.Partitions.EnsureCreatedAsync(database.Partitions.Resolve(from.AddMonths(-1)), default);
        foreach (var suffix in new[] { "", "202609" }) {
            var rows = Enumerable.Range(0, 1800).Select(index => Row("bulk-" + index, from.AddSeconds(index),
                index % 3 == 0 ? false : index % 3 == 1 ? true : null, index % 4 == 0 ? 123 : null,
                index % 2 == 0 ? ParcelProcessingStage.DwsReceived : ParcelProcessingStage.ScanUploaded) with {
                    RawPayload = new string('x', 2048)
                }).ToArray();
            await SaveAsync(database, suffix, rows);
        }
        await using var db = await database.Factory.CreateDbContextAsync();
        await using var processing = ParcelPartitionReadContext<ParcelProcessingStatisticsRow>.Create<ParcelProcessingRecord>(db, ["", "202609"]);
        var buckets = await ParcelProcessingStatisticsQuery.BuildQuery(processing, ["", "202609", "202609"], from, from.AddDays(1)).ToListAsync();
        Assert.Equal(2, buckets.Count);
        Assert.All(buckets, bucket => Assert.Equal(1800, bucket.Count));
        Assert.Equal(3600, buckets.Sum(bucket => bucket.Count));
        Assert.Equal(1200, buckets.Sum(bucket => bucket.Failed));
        Assert.Equal(900, buckets.Sum(bucket => bucket.UnboundDws));
        await AssertSingleCoveringRangePlanAsync(db, Assert.Single(capture.Commands), 2);
    }

    /// <summary>真实 SQLite 执行计划每个分表恰好一次时间范围覆盖索引检索，不含标量子查询或全表扫描。</summary>
    private static async Task AssertSingleCoveringRangePlanAsync(SortingHubDbContext db,
        (string Sql, (string Name, object Value, DbType Type)[] Parameters) captured, int partitionCount) {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + captured.Sql;
        foreach (var (name, value, type) in captured.Parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name; parameter.DbType = type; parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        var details = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        Assert.Equal(partitionCount, details.Count(detail => detail.Contains("SEARCH ", StringComparison.Ordinal)
            && detail.Contains("USING COVERING INDEX", StringComparison.Ordinal)
            && detail.Contains("OccurredAt>? AND OccurredAt<?", StringComparison.Ordinal)));
        Assert.DoesNotContain(details, detail => detail.Contains("SCALAR SUBQUERY", StringComparison.Ordinal));
        Assert.DoesNotContain(details, detail => detail.StartsWith("SCAN p", StringComparison.Ordinal));
    }

    /// <summary>仅向隔离测试库的指定周期写入已知事实。</summary>
    private static async Task SaveAsync(RelationalParcelTestDatabase database, string suffix, params ParcelProcessingRecord[] rows) {
        await using var db = await database.Partitions.CreateContextAsync(suffix, default);
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    /// <summary>构造没有报文投影依赖的统计样本。</summary>
    private static ParcelProcessingRecord Row(string key, DateTime occurred, bool? success, long? parcelId, ParcelProcessingStage stage) => new() {
        Key = key, RecordId = key, PayloadHash = key, SourceInstanceId = "direct-statistics", SourceRunId = "run",
        OccurredAt = occurred, RecordedAt = occurred, PartitionTime = occurred, IsSuccess = success, ParcelId = parcelId, Stage = stage
    };

}
