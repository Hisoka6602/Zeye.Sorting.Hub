using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>未关联处理记录的跨分表排序、旧数据兼容、有界报文加载和索引修复回归。</summary>
public sealed class UnboundProcessingQueryTests {
    /// <summary>旧基础表、月分表和日分表中的同时间记录统一排序，原始报文完整返回。</summary>
    [Fact]
    public async Task LatestRecordsAcrossMixedPartitionsIncludeLegacyRowsWithoutReceipts() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var start = new DateTime(2026, 9, 28, 10, 0, 0);
        var month = database.Partitions.Resolve(start);
        var day = ParcelPartitionPeriod.Resolve(start.AddDays(1), ParcelTimeShardingGranularity.PerDay);
        await database.Partitions.EnsureCreatedAsync(month, default);
        await database.Partitions.EnsureCreatedAsync(day, default);
        var payload = new string('报', 8192);
        foreach (var (suffix, key) in new[] { (string.Empty, "legacy-c"), (month.Suffix, "monthly-b"), (day.Suffix, "daily-a") }) {
            await using var db = await database.Partitions.CreateContextAsync(suffix, default);
            db.Add(Fact(key, start.AddDays(2), payload));
            db.AddRange(Enumerable.Range(0, 15).Select(index => Fact(key + index, start.AddSeconds(index), payload)));
            db.Add(Fact(key + "-bound", start.AddDays(3), payload) with { ParcelId = 123 });
            await db.SaveChangesAsync();
        }
        await database.Partitions.GetReadCatalogAsync(default);
        capture.Commands.Clear();

        var records = await database.Processing.GetUnboundAsync(3, default);

        Assert.Equal(new[] { "daily-a", "legacy-c", "monthly-b" }, records.Select(record => record.Key));
        Assert.All(records, record => { Assert.Null(record.ParcelId); Assert.Equal(payload, record.RawPayload); });
        var narrow = capture.Commands.Where(command => !command.Sql.Contains("RawPayload", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, narrow.Length);
        Assert.All(narrow, command => {
            Assert.Contains("LIMIT", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IS NULL", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UNION ALL", command.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ResponseBody", command.Sql, StringComparison.Ordinal);
        });
        Assert.Equal(3, capture.Commands.Count(command => command.Sql.Contains("RawPayload", StringComparison.Ordinal)));
    }

    /// <summary>实际仓储发出的候选查询走覆盖索引，不临时排序，也不访问原始报文列。</summary>
    [Fact]
    public async Task CandidateQueryUsesCoveringIndexWithoutTemporarySort() {
        var capture = new UnboundQueryCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        await database.Processing.GetUnboundAsync(20, default);
        var candidate = Assert.Single(capture.Commands);
        await using var db = await database.Factory.CreateDbContextAsync();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + candidate.Sql;
        foreach (var input in candidate.Parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = input.Name; parameter.Value = input.Value; parameter.DbType = input.Type;
            command.Parameters.Add(parameter);
        }
        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));

        Assert.Contains(plan, line => line.Contains("COVERING INDEX", StringComparison.OrdinalIgnoreCase)
            && line.Contains("ParcelId_RecordedAt_Key", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, line => line.Contains("TEMP B-TREE", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>已登记的历史分表缺少新索引时，由现有受隔离器保护的维护流程补齐。</summary>
    [Fact]
    public async Task ExistingPartitionMaintenanceRepairsUnboundIndex() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var period = database.Partitions.Resolve(new(2026, 9, 28, 10, 0, 0));
        await database.Partitions.EnsureCreatedAsync(period, default);
        string name;
        await using (var db = await database.Partitions.CreateContextAsync(period.Suffix, default)) {
            name = db.Model.FindEntityType(typeof(ParcelProcessingRecord))!.GetIndexes()
                .Single(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { "ParcelId", "RecordedAt", "Key" })).GetDatabaseName()!;
            var drop = new DropIndexOperation { Name = name, Table = "Parcel_ProcessingRecords_" + period.Suffix };
            foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate([drop]))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
        await database.Partitions.EnsureCreatedAsync(period, default, verifyExisting: true);
        await using var verify = await database.Factory.CreateDbContextAsync();
        var count = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {0}", name).SingleAsync();
        Assert.Equal(1, count);
    }

    /// <summary>空列表正常返回；非法数量和已取消请求都不访问处理记录数据。</summary>
    [Fact]
    public async Task EmptyInvalidAndCancelledRequestsKeepExistingContract() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        Assert.Empty(await database.Processing.GetUnboundAsync(200, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.Processing.GetUnboundAsync(0, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => database.Processing.GetUnboundAsync(201, default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.Processing.GetUnboundAsync(20, cancellation.Token));
    }

    /// <summary>创建无全局凭据的旧处理事实，验证查询不依赖新凭据索引完整性。</summary>
    private static ParcelProcessingRecord Fact(string key, DateTime time, string payload) => new() {
        Key = key, RecordId = key, PayloadHash = key, SourceInstanceId = "unbound-query-regression", SourceRunId = "qa",
        Stage = ParcelProcessingStage.DwsReceived, RecordedAt = time, OccurredAt = time, PartitionTime = time, RawPayload = payload
    };
}
