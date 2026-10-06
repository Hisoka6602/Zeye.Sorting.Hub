using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Zeye.Sorting.Hub.Application.Abstractions.Queries;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>实际关系数据库验证全量窗口，不以分页样本计算来源统计。</summary>
public sealed class ParcelWorkbenchTests {
    /// <summary>固定的本地统计终点，用于窗口边界断言。</summary>
    private static readonly DateTime End = new(2026, 10, 6, 1, 0, 0);

    /// <summary>超过200票、同名来源、跨编号会话、迟到扫码、历史基础表及边界全部计入。</summary>
    [Fact]
    public async Task CompleteWindowAggregatesAllSourcesAndPhysicalPartitions() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await SeedAsync(database);
        var report = await new ParcelWorkbenchReadService(database.Factory, database.Partitions).GetAsync(End, default);
        Assert.Equal(End.AddHours(-24), report.WindowStartLocal);
        Assert.Equal(End, report.WindowEndLocal);
        Assert.Equal(847, report.ParcelCount);
        Assert.Equal(7, report.UnassignedCount);
        Assert.Equal(3, report.Workstations.Count);
        var a = Assert.Single(report.Workstations, x => x.SourceInstanceId == "fusion-a");
        Assert.Equal("同名工作台", a.WorkstationName);
        Assert.Equal(751, a.ParcelCount);
        Assert.Equal(500, a.CompletedCount);
        Assert.Equal(201, a.PendingCount);
        Assert.Equal(40, a.ExceptionCount);
        Assert.Equal(10, a.OtherCount);
        var b = Assert.Single(report.Workstations, x => x.SourceInstanceId == "fusion-b");
        Assert.Equal(81, b.ParcelCount);
        Assert.Equal(80, b.CompletedCount);
        Assert.Equal(1, b.PendingCount);
        Assert.Equal(End, b.LastParcelAt);
        Assert.Equal(8, Assert.Single(report.Workstations, x => x.SourceInstanceId == null).ParcelCount);
        Assert.Equal(report.ParcelCount, report.Workstations.Sum(x => x.ParcelCount) + report.UnassignedCount);
    }

    /// <summary>点击卡片的明细在数据库内按实例过滤，同名来源及重复条码不会串票。</summary>
    [Fact]
    public async Task RecentSourceDetailsAreFilteredBeforePagination() {
        await using var database = new RelationalParcelTestDatabase("PerDay");
        await SeedAsync(database);
        var filter = new ParcelQueryFilter { SourceInstanceId = "fusion-b", ScannedTimeStart = End.AddHours(-24), ScannedTimeEnd = End };
        var page = await database.Parcels.GetPagedAsync(filter, new PageRequest { PageSize = 200, IncludeTotalCount = true }, default);
        Assert.Equal(81, page.TotalCount);
        Assert.Equal(81, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal("fusion-b", item.SourceInstanceId));
        var a = await database.Parcels.GetPagedAsync(filter with { SourceInstanceId = "fusion-a" }, new PageRequest { PageSize = 200, IncludeTotalCount = true }, default);
        Assert.Equal(751, a.TotalCount);
        Assert.Equal(200, a.Items.Count);
        Assert.All(a.Items, item => Assert.Equal("fusion-a", item.SourceInstanceId));
    }

    /// <summary>大小写不同的来源身份独立统计，历史空格归一后统计和明细保持一致。</summary>
    [Fact]
    public async Task SourceIdentityIsCaseSensitiveAndWhitespaceConsistent() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            Add(db, 2001, "fusion-a", "同名", End.AddHours(-1), End, ParcelStatus.Completed);
            Add(db, 2002, "fusion-A", "同名", End.AddHours(-1), End, ParcelStatus.Pending);
            Add(db, 2003, " fusion-a ", "同名", End.AddHours(-1), End, ParcelStatus.Completed);
            await db.SaveChangesAsync();
        }
        var report = await new ParcelWorkbenchReadService(database.Factory, database.Partitions).GetAsync(End, default);
        Assert.Equal(2, report.Workstations.Count);
        Assert.Equal(2, Assert.Single(report.Workstations, row => row.SourceInstanceId == "fusion-a").CompletedCount);
        Assert.Equal(1, Assert.Single(report.Workstations, row => row.SourceInstanceId == "fusion-A").PendingCount);
        var page = await database.Parcels.GetPagedAsync(new ParcelQueryFilter { SourceInstanceId = " fusion-a ",
            ScannedTimeStart = End.AddHours(-24), ScannedTimeEnd = End },
            new PageRequest { PageSize = 200, IncludeTotalCount = true }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal("fusion-a", item.SourceInstanceId!.Trim()));
    }

    /// <summary>空窗口不伪造工作台，并拒绝UTC或无效本地终点。</summary>
    [Fact]
    public async Task EmptyWindowAndLocalTimeBoundaryAreExplicit() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var reader = new ParcelWorkbenchReadService(database.Factory, database.Partitions);
        var empty = await reader.GetAsync(End, default);
        Assert.Equal(0, empty.ParcelCount);
        Assert.Equal(0, empty.UnassignedCount);
        Assert.Empty(empty.Workstations);
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetAsync(default, default));
        await Assert.ThrowsAsync<ArgumentException>(() => reader.GetAsync(DateTime.SpecifyKind(End, (DateTimeKind)1), default));
    }

    /// <summary>公开合同通过实际HTTP路由序列化本地时间及完整汇总。</summary>
    [Fact]
    public async Task WorkbenchRouteUsesCompleteWindowService() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IParcelWorkbenchReadService>(new ParcelWorkbenchReadService(database.Factory, database.Partitions));
        await using var app = builder.Build();
        app.MapParcelWorkbenchApis();
        await app.StartAsync();
        var response = await app.GetTestClient().GetFromJsonAsync<ParcelWorkbenchResponse>("/api/parcels/workbench");
        Assert.NotNull(response);
        Assert.Equal(TimeSpan.FromHours(24), response.WindowEndLocal - response.WindowStartLocal);
        Assert.Empty(response.Workstations);
    }

    /// <summary>创建跨基础表与物理分表的窗口、来源和状态边界数据。</summary>
    private static async Task SeedAsync(RelationalParcelTestDatabase database) {
        await database.InitializeAsync();
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            for (var id = 1; id <= 650; id++) {
                var status = id <= 500 ? ParcelStatus.Completed : id <= 600 ? ParcelStatus.Pending : id <= 640 ? ParcelStatus.SortingException : (ParcelStatus)99;
                Add(db, id, "fusion-a", "A旧名", End.AddHours(-12), End.AddHours(-23), status, id <= 325 ? "run-1" : "run-2");
            }
            for (var id = 1000; id < 1007; id++) Add(db, id, null, "", End.AddHours(-2), End, ParcelStatus.Pending);
            for (var id = 1010; id < 1018; id++) Add(db, id, null, "历史工作台", End.AddHours(-2), End, ParcelStatus.Completed);
            Add(db, 1020, "fusion-a", "A旧名", End.AddHours(-24), End.AddDays(-10), ParcelStatus.Pending);
            Add(db, 1021, "fusion-b", "同名工作台", End, End, ParcelStatus.Pending);
            Add(db, 1022, "fusion-b", "同名工作台", End.AddTicks(1), End, ParcelStatus.Pending);
            Add(db, 1023, "fusion-a", "A旧名", End.AddHours(-24).AddTicks(-1), End, ParcelStatus.Completed);
            await db.SaveChangesAsync();
        }
        var current = database.Partitions.Resolve(End.AddHours(-12));
        await database.Partitions.EnsureCreatedAsync(current, default);
        await using (var db = await database.Partitions.CreateContextAsync(current.Suffix, default)) {
            for (var id = 651; id <= 750; id++) Add(db, id, "fusion-a", "同名工作台", End.AddHours(-12), End.AddHours(-12), ParcelStatus.Pending);
            await db.SaveChangesAsync();
        }
        var historical = database.Partitions.Resolve(End.AddDays(-7));
        await database.Partitions.EnsureCreatedAsync(historical, default);
        await using (var db = await database.Partitions.CreateContextAsync(historical.Suffix, default)) {
            for (var id = 800; id < 880; id++) Add(db, id, "fusion-b", "同名工作台", End.AddHours(-11), End.AddDays(-7), ParcelStatus.Completed);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>历史数据种子特意包含缺失来源和未知状态，验证查询的兼容性。</summary>
    private static void Add(SortingHubDbContext db, long id, string? source, string name, DateTime scanned, DateTime created, ParcelStatus status, string run = "run-1") {
        var fact = new ParcelProcessingRecord { RecordId = "seed-" + id, SourceInstanceId = source?.Trim() ?? "legacy-fixture", SourceRunId = run,
            SourceParcelId = id, Stage = ParcelProcessingStage.Detected, OccurredAt = scanned, RecordedAt = created, PartitionTime = created, PayloadHash = "seed-" + id };
        var parcel = Parcel.CreateDetected(id, fact, created);
        var entry = db.Add(parcel);
        entry.Property(x => x.SourceInstanceId).CurrentValue = source;
        entry.Property(x => x.WorkstationName).CurrentValue = name;
        entry.Property(x => x.Status).CurrentValue = status;
        entry.Property(x => x.BarCodes).CurrentValue = "REPEATED-BARCODE";
    }
}
