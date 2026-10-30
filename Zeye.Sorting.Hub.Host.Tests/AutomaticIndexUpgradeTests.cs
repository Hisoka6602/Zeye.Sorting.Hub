using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>仅替换程序后的真实启动升级：基础表、旧周期、失败续跑与自动索引策略边界。</summary>
public sealed class AutomaticIndexUpgradeTests {
    /// <summary>本次自动升级覆盖的端点、DWS消息和检测索引名称前缀。</summary>
    private static readonly string[] UpgradeIndexPrefixes = ["IX_Processing_Parcel_Endpoint_Reliability",
        "IX_Dws_Message_Stage_Success", "IX_Processing_Parcel_Stage_Detection", "IX_Dws_Source_Time_Stage_Success", "IX_Processing_Source_Time_Stage_Success",
        "IX_Processing_Endpoint_Reliability_Covering"];

    /// <summary>结构操作、原始SQL及唯一性约束不进入普通索引自动执行路径。</summary>
    [Theory]
    [InlineData("index", true)]
    [InlineData("empty", false)]
    [InlineData("unique", false)]
    [InlineData("drop", false)]
    [InlineData("column", false)]
    [InlineData("sql", false)]
    [InlineData("mixed", false)]
    public void AutomaticUpgradeOnlyAcceptsOrdinaryIndexAdditions(string kind, bool expected) {
        MigrationOperation[] operations = kind switch {
            "empty" => [],
            "unique" => [new CreateIndexOperation { IsUnique = true }],
            "drop" => [new DropIndexOperation()],
            "column" => [new AddColumnOperation()],
            "sql" => [new SqlOperation { Sql = "CREATE INDEX ix ON t(c)" }],
            "mixed" => [new CreateIndexOperation(), new AddColumnOperation()],
            _ => [new CreateIndexOperation()]
        };
        Assert.Equal(expected, AutomaticIndexUpgradePolicy.Allows(new ConfigurationBuilder().Build(), operations));
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            [AutomaticIndexUpgradePolicy.ConfigurationKey] = "false"
        }).Build();
        Assert.False(AutomaticIndexUpgradePolicy.Allows(disabled, operations));
    }

    /// <summary>保留旧版预演与建表限制配置，程序重启自动升级基础表及历史分表；部分升级后重启能接续。</summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ExistingDatabaseAutomaticallyUpgradesIndexesAndResumesPartitions(bool baseAlreadyUpgraded, bool enabled) {
        using var storage = new ConfigurationTestStorage();
        var path = Path.Combine(storage.DirectoryPath, "business.db");
        var connection = $"Data Source={path};Pooling=False";
        var at = new DateTime(2026, 1, 2, 10, 0, 0);
        string suffix, latest;
        string[] baseIndexes, physicalIndexes;
        await using (var app = await DatabaseSetupTests.CreateInitializedDatabaseHostAsync(storage, "SQLite", connection, dryRun: false, isolateProviderCache: true)) {
            Assert.True(app.Services.GetRequiredService<DatabaseStartupState>().Ready);
            var factory = app.Services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>();
            var preparation = new ConfigurationBuilder().AddConfiguration(storage.Configuration).AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = "true",
                ["Persistence:Sharding:WriteRouting:DryRun"] = "false"
            }).Build();
            var partitions = new ParcelPartitionStore(factory, preparation);
            var period = partitions.Resolve(at); suffix = period.Suffix;
            await partitions.EnsureCreatedAsync(period, default);
            await using var db = await factory.CreateDbContextAsync();
            var migrations = db.Database.GetMigrations().ToArray(); latest = migrations[^1];
            var endpointMigration = migrations.Single(id => id.EndsWith("_AddParcelEndpointReliabilityIndex", StringComparison.Ordinal));
            var previous = migrations.TakeWhile(id => id != endpointMigration).Last();
            baseIndexes = UpgradeIndexNames(db);
            await using var physical = await partitions.CreateContextAsync(suffix, default);
            physicalIndexes = UpgradeIndexNames(physical);
            Assert.Equal(6, baseIndexes.Length); Assert.Equal(6, physicalIndexes.Length);
            var detected = Fact("detected", ParcelProcessingStage.Detected, at, at);
            var completed = Fact("completed", ParcelProcessingStage.SortingCompleted, at.AddSeconds(10), at);
            var parcel = Parcel.CreateDetected(1, detected, at);
            parcel.ApplyProcessingRecords([detected, completed]);
            physical.Add(parcel); physical.AddRange(detected, completed);
            await physical.SaveChangesAsync();
            foreach (var index in physicalIndexes)
                await physical.Database.ExecuteSqlRawAsync("DROP INDEX " + physical.GetService<ISqlGenerationHelper>().DelimitIdentifier(index));
            await PhysicalPartitionMigrationService.SaveVersionAsync(physical, "Parcel:" + suffix, previous, default);
            if (!baseAlreadyUpgraded) await db.GetService<IMigrator>().MigrateAsync(previous);
        }
        var overrides = new Dictionary<string, string?> { [AutomaticIndexUpgradePolicy.ConfigurationKey] = enabled.ToString() };
        for (var restart = 0; restart < (enabled ? 2 : 1); restart++) {
            await using var app = await DatabaseSetupTests.CreateInitializedDatabaseHostAsync(storage, "SQLite", connection, dryRun: true, overrides: overrides, isolateProviderCache: true);
            var state = app.Services.GetRequiredService<DatabaseStartupState>();
            Assert.Equal(enabled, state.Ready);
            Assert.Equal(!enabled, state.RequiresConfiguration);
            await using var db = await app.Services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
            foreach (var index in baseIndexes.Concat(physicalIndexes))
                Assert.Equal(enabled ? 1 : 0, await IndexCountAsync(db, index));
            if (enabled) {
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                Assert.Equal(latest, await db.Set<PartitionSchemaVersion>().Where(row => row.Key == "Parcel:" + suffix).Select(row => row.MigrationId).SingleAsync());
            }
            var partitions = app.Services.GetRequiredService<ParcelPartitionStore>();
            await using var physical = await partitions.CreateContextAsync(suffix, default);
            Assert.Equal(2, await physical.Set<ParcelProcessingRecord>().CountAsync());
            Assert.Equal(10000, await physical.Set<Parcel>().Select(row => row.LifecycleMilliseconds).SingleAsync());
        }
    }

    /// <summary>根据实际物理映射获取索引名称，兼容周期后缀及数据库名称长度限制。</summary>
    private static string[] UpgradeIndexNames(SortingHubDbContext db) => db.Model.GetEntityTypes().SelectMany(entity => entity.GetIndexes())
        .Select(index => index.GetDatabaseName()!).Where(name => UpgradeIndexPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))).ToArray();

    /// <summary>读取SQLite实际索引目录，验证DDL确实执行而非只更新模型或版本号。</summary>
    private static Task<long> IndexCountAsync(SortingHubDbContext db, string name) => db.Database.SqlQueryRaw<long>(
        "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = {0}", name).SingleAsync();

    /// <summary>保留可靠来源端点与固定入库锚点的历史测试事实。</summary>
    private static ParcelProcessingRecord Fact(string key, ParcelProcessingStage stage, DateTime at, DateTime registered) => new() {
        Key = key, RecordId = key, ParcelId = 1, SourceParcelId = 1, SourceInstanceId = "automatic-index-test", SourceRunId = "run-1",
        Stage = stage, OccurredAt = at, RecordedAt = registered, PartitionTime = registered, PayloadHash = key,
        HasReliableTimestamp = true, IsSuccess = true, TargetChuteCode = "01", ActualChuteCode = "01"
    };
}
