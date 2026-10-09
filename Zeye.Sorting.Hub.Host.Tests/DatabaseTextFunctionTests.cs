using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>文本规范化与继续游标在四种数据库及分表读模型中保持服务端执行。</summary>
public sealed class DatabaseTextFunctionTests {
    /// <summary>数据库查询仍生成 LOWER 和列比较，不生成客户端比较或不存在的用户函数。</summary>
    [Theory]
    [InlineData("MySql")]
    [InlineData("SqlServer")]
    [InlineData("Oracle")]
    [InlineData("SQLite")]
    public void EveryProviderTranslatesTextAndCursorInBothModels(string provider) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        // 沿用已有生产选项及 EF 服务缓存，避免为同一提供器额外建立设计时服务组合。
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSortingHubPersistence(configuration).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        using var read = ParcelPartitionReadContext<ManagedDocument>.Create<ManagedDocument>(db, [""]);
        AssertTranslated(Filter(db.Set<ManagedDocument>()).ToQueryString());
        AssertTranslated(Filter(read.QueryAll([""], source => source)).ToQueryString());
    }

    /// <summary>土耳其语进程环境不改变数据库大小写与游标结果，普通模型和分表只读模型返回一致记录。</summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public async Task SqliteTextAndCursorResultsDoNotDependOnProcessCulture(string culture) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            await using var database = new RelationalParcelTestDatabase();
            await database.InitializeAsync();
            await using var db = await database.Factory.CreateDbContextAsync();
            db.AddRange(new ManagedDocument { Key = "000A", Json = "NOREAD" },
                new ManagedDocument { Key = "000B", Json = "NoRead" },
                new ManagedDocument { Key = "000C", Json = "noread" },
                new ManagedDocument { Key = "000D", Json = "Read" });
            await db.SaveChangesAsync();
            var values = await Filter(db.Set<ManagedDocument>()).Select(row => row.Key).ToArrayAsync();
            Assert.Collection(values, item => Assert.Equal("000B", item), item => Assert.Equal("000C", item));
            await using var read = ParcelPartitionReadContext<ManagedDocument>.Create<ManagedDocument>(db, [""]);
            Assert.Equal(values, await Filter(read.QueryAll([""], source => source)).Select(row => row.Key).ToArrayAsync());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>同一服务端筛选用于普通查询及共享只读模型。</summary>
    private static IOrderedQueryable<ManagedDocument> Filter(IQueryable<ManagedDocument> source) => source
        .Where(row => DatabaseTextFunctions.Lower(row.Json) == "noread" && DatabaseTextFunctions.IsAfter(row.Key, "000A"))
        .OrderBy(row => row.Key);

    /// <summary>验证 SQL 运算位置及不存在客户端函数名称。</summary>
    private static void AssertTranslated(string sql) {
        Assert.Contains("LOWER(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" > ", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(DatabaseTextFunctions.IsAfter), sql, StringComparison.Ordinal);
    }
}
