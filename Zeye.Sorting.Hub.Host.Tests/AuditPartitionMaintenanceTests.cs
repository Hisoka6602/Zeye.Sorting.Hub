using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>实际关系数据库验证日表预建、数据保留、断点重试及建表隔离。</summary>
public sealed class AuditPartitionMaintenanceTests {
    /// <summary>创建测试专用的建表隔离配置。</summary>
    private static IConfiguration Configuration(bool allowed = true, bool dryRun = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = allowed.ToString(),
        ["Persistence:Sharding:WriteRouting:DryRun"] = dryRun.ToString()
    }).Build();

    /// <summary>有界预建窗口跨年时仍覆盖下一日。</summary>
    [Fact]
    public void WindowIncludesNextDayAndCrossesYearBoundary() {
        var start = LocalTimeTestConstraint.CreateLocalTime(2026, 12, 31, 23, 0, 0);
        var days = AuditPartitionMaintenanceService.Plan(start, 48);
        Assert.Equal(3, days.Count);
        Assert.Equal(start.Date, days[0]);
        Assert.Equal(start.AddDays(2).Date, days[^1]);
        Assert.Equal(2, AuditPartitionMaintenanceService.Plan(start.Date, 1).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditPartitionMaintenanceService.Plan(start, 169));
    }

    /// <summary>未授权或仍处于预演时拒绝任何实际建表。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CreationRequiresBothExplicitGates(bool allowed, bool dryRun) {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var service = new AuditPartitionMaintenanceService(database.Factory, Configuration(allowed, dryRun));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCreatedAsync(DateTime.Today, default));
        await using var db = await database.Factory.CreateDbContextAsync();
        var count = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name LIKE 'WebRequestAuditLogs_%'").SingleAsync();
        Assert.Equal(0, count);
    }

    /// <summary>成对日表可跨日期建立，缺索引可重试补齐且保留已有行。</summary>
    [Fact]
    public async Task CreatesPairAndIndexesOnMultipleDaysPreservingRowsAcrossRetries() {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var service = new AuditPartitionMaintenanceService(database.Factory, Configuration());
        var day = LocalTimeTestConstraint.CreateLocalTime(2026, 10, 3, 0, 0, 0);
        await service.EnsureCreatedAsync(day, default);
        await service.EnsureCreatedAsync(day.AddDays(1), default);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(new WebRequestAuditLog { Id = 42, TraceId = "retained", StartedAt = day });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"WebRequestAuditLogs_20261003\" SELECT * FROM \"WebRequestAuditLogs\"");
            await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_WebRequestAuditLogs_StartedAt_20261003\"");
        }
        await Task.WhenAll(service.EnsureCreatedAsync(day, default), service.EnsureCreatedAsync(day, default));
        Assert.Equal(1, await database.CountPhysicalAsync("WebRequestAuditLogs_20261003"));
        Assert.Equal(0, await database.CountPhysicalAsync("WebRequestAuditLogDetails_20261003"));
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            var count = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='index' AND name='IX_WebRequestAuditLogs_StartedAt_20261003'").SingleAsync();
            Assert.Equal(1, count);
        }
    }

    /// <summary>不兼容旧结构必须报告失败并保留已有数据。</summary>
    [Fact]
    public async Task IncompatibleExistingTableFailsWithoutOverwritingRows() {
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var day = LocalTimeTestConstraint.CreateLocalTime(2026, 10, 3, 0, 0, 0);
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"WebRequestAuditLogs_20261003\" (Id INTEGER PRIMARY KEY)");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"WebRequestAuditLogs_20261003\" VALUES (42)");
        }
        var service = new AuditPartitionMaintenanceService(database.Factory, Configuration());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureCreatedAsync(day, default));
        Assert.Equal(1, await database.CountPhysicalAsync("WebRequestAuditLogs_20261003"));
    }
}
