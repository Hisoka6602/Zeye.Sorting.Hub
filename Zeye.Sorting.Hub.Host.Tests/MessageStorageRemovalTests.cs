using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
        Assert.False(context.Database.HasPendingModelChanges());
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

    /// <summary>创建不连接任何业务库的数据库模型上下文。</summary>
    private static SortingHubDbContext CreateContext(bool sqlServer) {
        if (!sqlServer) return new MySqlContextFactory().CreateDbContext(["--provider", "MySql"]);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;Encrypt=False;"
        }).Build();
        return new SqlServerContextFactory().CreateDbContext(configuration);
    }
}
