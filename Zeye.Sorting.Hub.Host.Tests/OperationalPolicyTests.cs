using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
namespace Zeye.Sorting.Hub.Host.Tests;
/// <summary>运维策略持久化、版本冲突及分表幂等执行回归。</summary>
public sealed class OperationalPolicyTests {
    /// <summary>新的服务实例读取保存策略，旧版本不得覆盖，非法窗口被拒绝。</summary>
    [Fact]
    public async Task PolicySurvivesReloadAndRejectsStaleOrUnboundedChanges() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var backups = Microsoft.Extensions.Options.Options.Create(new BackupOptions()); var sharding = Microsoft.Extensions.Options.Options.Create(new ShardingPrebuildOptions());
        var service = new OperationalPolicyService(db.Factory, backups, sharding);
        var initial = await service.ReadAsync(default); Assert.False(initial.AutomaticBackups);
        var saved = await service.WriteAsync(initial with { AutomaticBackups = true, BackupIntervalMinutes = 30, PrebuildAheadHours = 48 }, default);
        Assert.NotNull(saved); Assert.Equal(1, saved.Revision); Assert.Null(await service.WriteAsync(initial, default));
        Assert.Equal(saved, await new OperationalPolicyService(db.Factory, backups, sharding).ReadAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(saved with { BackupIntervalMinutes = 0 }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.WriteAsync(saved with { PrebuildAheadHours = 8760 }, default));
    }
    /// <summary>实际预建在新关系数据库中创建当前及下一周期，重复执行保留相同目录。</summary>
    [Fact]
    public async Task PrebuildCreatesRealPartitionsAndRepeatsWithoutDataLoss() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var service = new PartitionMaintenanceService(db.Partitions, db.Factory, Microsoft.Extensions.Options.Options.Create(new ShardingPrebuildOptions()));
        var planned = service.Plan(); Assert.True(planned.Count >= 2);
        await service.ExecuteAsync(default); var before = await db.Partitions.GetReadSuffixesAsync(default);
        await service.ExecuteAsync(default); var after = await db.Partitions.GetReadSuffixesAsync(default);
        Assert.Equal(before, after); foreach (var period in planned) Assert.Contains(period.Suffix, after);
    }

    /// <summary>目录已存在但关联表或索引缺失时，自动复核实际结构并补齐。</summary>
    [Fact]
    public async Task PrebuildRepairsMissingTableBehindExistingCatalog() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var service = new PartitionMaintenanceService(db.Partitions, db.Factory, Microsoft.Extensions.Options.Options.Create(new ShardingPrebuildOptions()));
        await service.ExecuteAsync(default);
        var suffix = service.Plan()[0].Suffix;
        await using (var context = await db.Factory.CreateDbContextAsync()) {
            await context.Database.ExecuteSqlRawAsync("DROP TABLE \"Parcel_ImageInfos_" + suffix + "\"");
        }
        var before = await db.Partitions.GetReadSuffixesAsync(default);
        await service.ExecuteAsync(default);
        Assert.Equal(before, await db.Partitions.GetReadSuffixesAsync(default));
        Assert.Equal(0, await db.CountPhysicalAsync("Parcel_ImageInfos_" + suffix));
    }
}
