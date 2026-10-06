using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证移除消息投递模块后的模型一致性及已有数据库升级范围。</summary>
public sealed class MessageStorageRemovalTests {
    /// <summary>两种数据库均不再映射投递消息，快照与运行模型保持一致。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentModelExcludesRetiredMessageStorage(bool sqlServer) {
        using var context = CreateContext(sqlServer);
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.GetTableName() == "OutboxMessages");
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var operations = context.GetService<IMigrationsModelDiffer>().GetDifferences(snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());
        var differences = string.Join("; ", operations.Select(operation => operation switch {
            CreateIndexOperation index => $"新增索引 {index.Table}.{index.Name}: {string.Join(",", index.Columns)}",
            DropIndexOperation index => $"移除索引 {index.Table}.{index.Name}",
            AlterColumnOperation column => $"修改字段 {column.Table}.{column.Name}: {column.ColumnType}",
            _ => operation.GetType().Name
        }));
        Assert.False(context.Database.HasPendingModelChanges(), differences);
        Assert.Contains(context.Model.GetEntityTypes(), entity => entity.GetTableName() == "Parcels");
    }

    /// <summary>升级只处理已退役的消息表，非空表保留记录，回滚仍能恢复旧结构。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpgradeOnlyCleansEmptyRetiredMessageStorage(bool sqlServer) {
        using var context = CreateContext(sqlServer);
        var assembly = context.GetService<IMigrationsAssembly>();
        var entry = Assert.Single(assembly.Migrations.Where(item => item.Key.Contains("RemoveRetiredMessageStorage", StringComparison.Ordinal)));
        var migration = assembly.CreateMigration(entry.Value, context.Database.ProviderName!);
        var operation = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        Assert.Contains("OutboxMessages", operation.Sql, StringComparison.Ordinal);
        Assert.Contains(sqlServer ? "IF NOT EXISTS" : "'SELECT 1'", operation.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Parcels", operation.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ManagedDocuments", operation.Sql, StringComparison.Ordinal);
        var restoredTable = Assert.Single(migration.DownOperations.OfType<CreateTableOperation>());
        Assert.Equal("OutboxMessages", restoredTable.Name);
    }

    /// <summary>两种数据库的报表覆盖索引只能由一条迁移创建，避免升级时重复键失败。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoveringIndexesAreCreatedByExactlyOneMigration(bool sqlServer) {
        using var context = CreateContext(sqlServer);
        var assembly = context.GetService<IMigrationsAssembly>();
        if (!sqlServer) {
            Assert.Contains("20261005193231_AddAnalyticsCoveringIndexes", assembly.Migrations.Keys);
            Assert.DoesNotContain("20261006033000_AddAnalyticsCoveringIndexes", assembly.Migrations.Keys);
        }
        var operations = assembly.Migrations.SelectMany(entry =>
            assembly.CreateMigration(entry.Value, context.Database.ProviderName!).UpOperations)
            .OfType<CreateIndexOperation>().ToArray();
        foreach (var name in new[] {
            "IX_Parcels_CreatedTime_Id_SourceParcelId_DetectedTime",
            "IX_Parcels_CompletedTime_Status_SourceParcelId_DetectedTime",
            "IX_Parcel_ProcessingRecords_OccurredAt_IsSuccess_ParcelId_Stage"
        }) Assert.Single(operations.Where(index => index.Name == name));
    }

    /// <summary>创建不连接任何业务库的数据库模型上下文。</summary>
    private static SortingHubDbContext CreateContext(bool sqlServer) {
        if (!sqlServer) return new MySqlContextFactory().CreateDbContext(["--provider", "MySql"]);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;Encrypt=False;"
        }).Build();
        return new SqlServerContextFactory().CreateDbContext(configuration);
    }
}
