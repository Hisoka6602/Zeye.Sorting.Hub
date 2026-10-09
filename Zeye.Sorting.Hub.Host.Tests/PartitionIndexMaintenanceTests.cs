using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证覆盖索引在线生成、已有物理索引幂等维护和维护耗时证据保留。</summary>
public sealed class PartitionIndexMaintenanceTests {
    /// <summary>目标索引名称与当前 EF Core 模型保持一致。</summary>
    private const string DurationIndexName = "IX_Processing_Stage_PartitionTime_Duration";

    /// <summary>完整迁移与混合命令保持顺序及事务边界，只有目标索引增加在线选项。</summary>
    [Fact]
    public void OnlineIndexAdapterPreservesBatchOrderAndMigrationScripts() {
        using var db = MySqlContextFactory.CreateConfiguredContext(BuildConfiguration());
        var generator = db.GetService<IMigrationsSqlGenerator>();
        MigrationOperation[] operations = [
            new SqlOperation { Sql = "SELECT 1;", SuppressTransaction = true },
            new CreateIndexOperation { Table = "Parcel_ProcessingRecords", Name = DurationIndexName, Columns = ["Stage"] },
            new CreateIndexOperation { Table = "Parcel_ProcessingRecords", Name = "IX_unmodified", Columns = ["RecordId"] },
            new SqlOperation { Sql = "SELECT 2;" }
        ];
        var expected = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<Pomelo.EntityFrameworkCore.MySql.Migrations.MySqlMigrationsSqlGenerator>(db.GetInfrastructure())
            .Generate(operations);
        var actual = generator.Generate(operations);
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < actual.Count; index++) {
            Assert.Equal(expected[index].TransactionSuppressed, actual[index].TransactionSuppressed);
            Assert.Equal(expected[index].CommandText, actual[index].CommandText.Replace(" ALGORITHM=INPLACE LOCK=NONE", "", StringComparison.Ordinal));
        }
        Assert.Single(actual, command => command.CommandText.Contains(" ALGORITHM=INPLACE LOCK=NONE", StringComparison.Ordinal));
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains(" ALGORITHM=INPLACE LOCK=NONE;", script, StringComparison.Ordinal);
    }

    /// <summary>基础表及两个历史分表使用同一 EF 生成器；运行期与设计时脚本均显式要求在线创建。</summary>
    [Theory]
    [InlineData(false, "", DurationIndexName)]
    [InlineData(false, "202609", DurationIndexName)]
    [InlineData(false, "202610", DurationIndexName)]
    [InlineData(true, "", DurationIndexName)]
    [InlineData(true, "202609", DurationIndexName)]
    [InlineData(true, "202610", DurationIndexName)]
    [InlineData(false, "", "IX_Processing_Source_Stage_Duration")]
    [InlineData(false, "202609", "IX_Processing_Source_Stage_Duration")]
    [InlineData(false, "202610", "IX_Processing_Source_Stage_Duration")]
    [InlineData(true, "", "IX_Processing_Source_Stage_Duration")]
    [InlineData(true, "202609", "IX_Processing_Source_Stage_Duration")]
    [InlineData(true, "202610", "IX_Processing_Source_Stage_Duration")]
    [InlineData(false, "", "IX_Dws_Time_Stage_Success")]
    [InlineData(false, "202609", "IX_Dws_Time_Stage_Success")]
    [InlineData(false, "202610", "IX_Dws_Time_Stage_Success")]
    [InlineData(true, "", "IX_Dws_Time_Stage_Success")]
    [InlineData(true, "202609", "IX_Dws_Time_Stage_Success")]
    [InlineData(true, "202610", "IX_Dws_Time_Stage_Success")]
    public void DurationIndexUsesOnlineDdlInRuntimeAndDesignTime(bool designTime, string suffix, string indexName) {
        var configuration = BuildConfiguration();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSortingHubPersistence(configuration).BuildServiceProvider();
        using var template = designTime ? MySqlContextFactory.CreateConfiguredContext(configuration)
            : services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        var options = new DbContextOptionsBuilder<SortingHubDbContext>((DbContextOptions<SortingHubDbContext>)template.GetService<IDbContextOptions>())
            .ReplaceService<IModelCacheKeyFactory, ParcelPartitionModelCacheKeyFactory>().Options;
        using var db = new SortingHubDbContext(options) { ParcelPartitionSuffix = suffix };
        var model = db.GetService<IDesignTimeModel>().Model;
        var operation = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel())
            .OfType<CreateIndexOperation>().Single(index => index.Name == indexName + (suffix.Length == 0 ? "" : "_" + suffix));
        var generator = Assert.IsType<MySqlOnlineIndexMigrationsSqlGenerator>(db.GetService<IMigrationsSqlGenerator>());
        var command = Assert.Single(generator.Generate([operation], model));
        Assert.Contains(" ALGORITHM=INPLACE LOCK=NONE;", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("`" + operation.Table + "`", command.CommandText, StringComparison.Ordinal);
        Assert.Equal(indexName == "IX_Dws_Time_Stage_Success" ? 3 : 10, operation.Columns.Length);
        if (suffix.Length == 0) Assert.False(db.Database.HasPendingModelChanges());
    }

    /// <summary>普通其他索引及全文、空间、唯一索引不被目标优化改变，删除索引仍按提供器默认生成。</summary>
    [Theory]
    [InlineData("other", false, null)]
    [InlineData(DurationIndexName, true, null)]
    [InlineData(DurationIndexName, false, "MySql:FullTextIndex")]
    [InlineData(DurationIndexName, false, "MySql:SpatialIndex")]
    public void OtherIndexOperationsKeepProviderDefaults(string name, bool unique, string? annotation) {
        using var db = MySqlContextFactory.CreateConfiguredContext(BuildConfiguration());
        var operation = new CreateIndexOperation { Table = "Parcel_ProcessingRecords", Name = name, Columns = ["Stage"], IsUnique = unique };
        if (annotation is not null) operation[annotation] = true;
        var generator = db.GetService<IMigrationsSqlGenerator>();
        var command = Assert.Single(generator.Generate([operation]));
        Assert.DoesNotContain("ALGORITHM=", command.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOCK=", command.CommandText, StringComparison.OrdinalIgnoreCase);
        var drop = Assert.Single(generator.Generate([new DropIndexOperation { Table = operation.Table, Name = operation.Name }]));
        Assert.DoesNotContain("ALGORITHM=", drop.CommandText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>已有两个月的索引反复复核不会重建；部分索引缺失时只补一次该索引。</summary>
    [Fact]
    public async Task HistoricalIndexVerificationIsIdempotentAndRepairsOnlyMissingIndex() {
        var capture = new PartitionIndexCaptureInterceptor();
        await using var database = new RelationalParcelTestDatabase(queryInterceptor: capture);
        await database.InitializeAsync();
        var september = database.Partitions.Resolve(new DateTime(2026, 9, 1));
        var october = database.Partitions.Resolve(new DateTime(2026, 10, 1));
        await database.Partitions.EnsureCreatedAsync(september, default);
        await database.Partitions.EnsureCreatedAsync(october, default);
        Assert.Single(capture.Commands, command => command.Contains(DurationIndexName + "_202609", StringComparison.Ordinal));
        Assert.Single(capture.Commands, command => command.Contains(DurationIndexName + "_202610", StringComparison.Ordinal));
        capture.Commands.Clear();
        await database.Partitions.EnsureCreatedAsync(september, default, verifyExisting: true);
        await database.Partitions.EnsureCreatedAsync(october, default, verifyExisting: true);
        Assert.Empty(capture.Commands);

        await using (var db = await database.Partitions.CreateContextAsync("202610", default)) {
            var drop = new DropIndexOperation { Table = "Parcel_ProcessingRecords_202610", Name = DurationIndexName + "_202610" };
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate([drop]);
            await db.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, db.GetService<IRelationalConnection>());
        }
        capture.Commands.Clear();
        await database.Partitions.EnsureCreatedAsync(october, default, verifyExisting: true);
        var repair = Assert.Single(capture.Commands);
        Assert.Contains(DurationIndexName + "_202610", repair, StringComparison.Ordinal);
        capture.Commands.Clear();
        await database.Partitions.EnsureCreatedAsync(october, default, verifyExisting: true);
        // 模拟另一个进程没有就绪缓存，仍须通过元数据确认并跳过已存在的索引。
        var freshStore = new ParcelPartitionStore(database.Factory, new ConfigurationBuilder().Build());
        await freshStore.EnsureCreatedAsync(september, default, verifyExisting: true);
        await freshStore.EnsureCreatedAsync(october, default, verifyExisting: true);
        Assert.Empty(capture.Commands);
    }

    /// <summary>优化不会删除或过滤慢 DDL 记录；成功耗时和失败超时仍可查证。</summary>
    [Fact]
    public void SlowDdlEvidenceIsRetained() {
        var store = new SlowQueryProfileStore(BuildConfiguration());
        const string command = "CREATE INDEX `IX_Processing_Stage_PartitionTime_Duration_202610` ON `Parcel_ProcessingRecords_202610` (`Stage`) ALGORITHM=INPLACE LOCK=NONE;";
        store.Record(command, TimeSpan.FromTicks(85012400));
        store.Record(command, TimeSpan.FromTicks(23001000), exception: new TimeoutException());
        var profile = Assert.Single(store.GetTopProfiles().Items);
        Assert.Equal(2, profile.CallCount);
        Assert.Equal(1, profile.ErrorCount);
        Assert.Equal(1, profile.TimeoutCount);
        Assert.Equal(8501.24m, profile.MaxMilliseconds);
    }

    /// <summary>离线模型分析配置，不连接实际业务数据库。</summary>
    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Persistence:Provider"] = "MySql", ["Persistence:MySql:ServerVersion"] = "8.4.0",
        ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
        ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "500",
        ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "true"
    }).Build();
}
