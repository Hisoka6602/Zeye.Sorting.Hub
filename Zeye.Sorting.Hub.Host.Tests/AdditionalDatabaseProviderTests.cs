using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>四数据库注册、独立迁移快照及不产生隐式文件的回归验证。</summary>
public sealed class AdditionalDatabaseProviderTests {
    /// <summary>运行配置必须选择对应驱动和迁移快照，模型不能有未登记差异。</summary>
    [Theory]
    [InlineData("MySql", "Pomelo.EntityFrameworkCore.MySql", "Zeye.Sorting.Hub.Infrastructure")]
    [InlineData("SqlServer", "Microsoft.EntityFrameworkCore.SqlServer", "Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations")]
    [InlineData("Oracle", "Oracle.EntityFrameworkCore", "Zeye.Sorting.Hub.Infrastructure.OracleMigrations")]
    [InlineData("SQLite", "Microsoft.EntityFrameworkCore.Sqlite", "Zeye.Sorting.Hub.Infrastructure.SqliteMigrations")]
    public void RuntimeProviderSelectsIndependentSnapshot(string provider, string driver, string migrationAssembly) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = provider, ["Persistence:MySql:ServerVersion"] = "8.4.0",
            ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Database=design_time_only;User Id=design_time_only",
            ["ConnectionStrings:SqlServer"] = "Server=127.0.0.1;Database=design_time_only;Integrated Security=True;TrustServerCertificate=True",
            ["ConnectionStrings:Oracle"] = "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1",
            ["ConnectionStrings:SQLite"] = "Data Source=data/business/design-time-only.db"
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration).BuildServiceProvider();
        using var db = services.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContext();
        Assert.Equal(driver, db.Database.ProviderName);
        Assert.Equal(migrationAssembly, db.GetService<IMigrationsAssembly>().Assembly.GetName().Name);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    /// <summary>SQLite 缺失文件的只读初始化探测不能创建文件或目录。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteProbeDoesNotCreateMissingDirectoryOrFile(bool parentExists) {
        using var environment = new ConfigurationTestStorage();
        var directory = Path.Combine(environment.DirectoryPath, "nested");
        if (parentExists) Directory.CreateDirectory(directory);
        var configuration = new ConfigurationBuilder().Build();
        var dialect = new SqliteDialect(configuration);
        var databasePath = Path.Combine(directory, "business.db");
        var connection = "Data Source=" + databasePath;
        await using var administration = dialect.CreateAdministrationConnection(connection);
        await DatabaseConnectionOpenCoordinator.ProbeAdministrationConnectionAsync(dialect, connection, CancellationToken.None);
        Assert.False(await dialect.DatabaseExistsAsync(administration, dialect.ExtractDatabaseName(connection), CancellationToken.None));
        Assert.Equal(parentExists, Directory.Exists(directory));
        Assert.False(File.Exists(databasePath));
    }

    /// <summary>禁止创建文件的 SQLite 模式仍须在启动探测时报告缺失文件，不能被当成可初始化的新库。</summary>
    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("ReadWrite")]
    public async Task SqliteStartupProbeRejectsMissingFileWhenCreationIsDisabled(string mode) {
        using var environment = new ConfigurationTestStorage();
        var path = Path.Combine(environment.DirectoryPath, "missing", "business.db");
        await Assert.ThrowsAsync<SqliteException>(() => DatabaseConnectionOpenCoordinator.ProbeAdministrationConnectionAsync(
            new SqliteDialect(environment.Configuration), $"Data Source={path};Mode={mode};Pooling=False", CancellationToken.None));
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    /// <summary>持久化业务数据拒绝内存模式，提供器名称兼容旧写法。</summary>
    [Fact]
    public void ProviderValidationRejectsVolatileBusinessStorage() {
        Assert.Throws<InvalidOperationException>(() => AdditionalDbContextOptions.NormalizeSqliteConnectionString("Data Source=:memory:"));
        Assert.Equal("SqlServer", ConfiguredProviderNames.Normalize("mssql"));
        Assert.Equal("SQLite", ConfiguredProviderNames.Normalize("Sqlite"));
    }

    /// <summary>只读连接遗漏 Mode 时也不能在健康探针中隐式创建业务文件。</summary>
    [Fact]
    public async Task SqliteReadOnlyRouteDoesNotCreateMissingFile() {
        var directory = Path.Combine(Path.GetTempPath(), "zeye-readonly-probe-" + Guid.NewGuid().ToString("N"));
        var connection = "Data Source=" + Path.Combine(directory, "business.db");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:Provider"] = "Sqlite", ["ConnectionStrings:SQLite"] = connection, ["ConnectionStrings:SQLiteReadOnly"] = connection,
            ["Persistence:ReadOnlyDatabase:IsEnabled"] = "true", ["Persistence:ReadOnlyDatabase:FallbackToPrimaryWhenUnavailable"] = "false"
        }).Build();
        await using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration).BuildServiceProvider();
        var selector = services.GetRequiredService<ReadOnlyDbContextFactorySelector>();
        await using var context = await selector.CreateDbContextAsync(CancellationToken.None);
        Assert.Equal(SqliteOpenMode.ReadOnly, new SqliteConnectionStringBuilder(context.Database.GetConnectionString()).Mode);
        Assert.False((await selector.ProbeRouteAsync(CancellationToken.None)).IsReadOnlyAvailable);
        Assert.False(Directory.Exists(directory));
    }

    /// <summary>控制字符前缀必须按字节语义判断，文化比较不能导致所有正常字符串被扩长。</summary>
    [Fact]
    public void OracleStringEncodingPreservesBoundedIdentifiersAndEmptyValues() {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>();
        AdditionalDbContextOptions.Configure(options, "Oracle", "User Id=design_time_only;Password=design_time_only;Data Source=localhost/FREEPDB1");
        using var database = new SortingHubDbContext(options.Options);
        var converter = database.Model.FindEntityType(typeof(Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion.FusionSourceLease))!.FindProperty("JournalId")!.GetValueConverter()!;
        var identity = new string('a', 32);
        Assert.Equal(identity, converter.ConvertToProvider(identity));
        Assert.Equal(identity, converter.ConvertFromProvider(identity));
        foreach (var value in new[] { "", "中文", "\u0001原文" }) Assert.Equal(value, converter.ConvertFromProvider(converter.ConvertToProvider(value)));
    }
}
