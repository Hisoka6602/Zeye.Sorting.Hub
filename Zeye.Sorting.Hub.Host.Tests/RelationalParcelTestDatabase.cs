using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>独立SQLite文件数据库，实际验证关系映射、物理表与事务。</summary>
public sealed class RelationalParcelTestDatabase : IAsyncDisposable {
    /// <summary>测试专用数据库文件。</summary>
    private readonly string _path = Path.GetTempFileName();
    /// <summary>测试上下文工厂。</summary>
    public IDbContextFactory<SortingHubDbContext> Factory { get; }
    /// <summary>实际物理分表服务。</summary>
    public ParcelPartitionStore Partitions { get; }
    /// <summary>处理事实仓储。</summary>
    public ParcelProcessingRepository Processing { get; }
    /// <summary>包裹详情与列表仓储。</summary>
    public ParcelRepository Parcels { get; }
    /// <summary>可控事务提交失败注入。</summary>
    public ParcelCommitFailureInterceptor Failure { get; } = new();
    /// <summary>真实元数据查询计数，验证预热之后不在热处理链路加载配置。</summary>
    public ParcelMetadataIoInterceptor MetadataIo { get; } = new();

    /// <summary>配置测试专用数据库和允许执行的DDL隔离器。</summary>
    public RelationalParcelTestDatabase(string? granularity = null, IInterceptor? queryInterceptor = null) {
        // 与生产工厂保持一致，验证写仓储显式启用跟踪或执行数据库更新。
        // 每个测试使用独立原生连接，不清空其他并发测试使用的全进程 SQLite 连接池。
        var options = new DbContextOptionsBuilder<SortingHubDbContext>().UseSqlite("Data Source=" + _path + ";Pooling=False")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).AddInterceptors(Failure, MetadataIo);
        if (queryInterceptor is not null) options.AddInterceptors(queryInterceptor);
        Factory = new PooledDbContextFactory<SortingHubDbContext>(options.Options);
        var settings = new Dictionary<string, string?> {
            ["Persistence:Sharding:WriteRouting:AllowTableCreation"] = "true",
            ["Persistence:Sharding:WriteRouting:DryRun"] = "false"
        };
        if (granularity is not null) settings["Persistence:Sharding:Strategy:Time:Granularity"] = granularity;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Partitions = new(Factory, configuration);
        Processing = new(Factory, Partitions);
        Parcels = new(Factory, configuration, Partitions);
    }

    /// <summary>建立基础模型与全局目录，再由路由服务创建物理周期表。</summary>
    public async Task InitializeAsync() {
        await using var db = await Factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>读取某个物理分表的实际行数。</summary>
    public async Task<long> CountPhysicalAsync(string table) {
        if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Za-z0-9_]+$")) throw new ArgumentException("测试表名无效。");
        await using var db = await Factory.CreateDbContextAsync();
        return await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM \"" + table + "\"").SingleAsync();
    }

    /// <summary>删除当前测试生成的单个数据库文件，不干扰其他测试的连接池。</summary>
    public ValueTask DisposeAsync() {
        File.Delete(_path);
        return ValueTask.CompletedTask;
    }
}
