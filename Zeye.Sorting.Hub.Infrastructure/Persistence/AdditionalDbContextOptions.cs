using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore.Infrastructure;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>Oracle、SQLite 的运行期与设计时模型选项，保证迁移快照与生产模型一致。</summary>
public static class AdditionalDbContextOptions {
    /// <summary>配置新增提供器；超时范围为 1..600 秒，SQLite 相对路径以内容根目录为基准。</summary>
    public static void Configure(DbContextOptionsBuilder options, string provider, string connectionString, string? baseDirectory = null, int timeoutSeconds = 30) {
        if (ConfiguredProviderNames.Normalize(provider) == ConfiguredProviderNames.Oracle) {
            options.AddInterceptors(OracleConnectionLivenessInterceptor.Instance);
            options.UseOracle(OracleConnectionStringNormalization.Normalize(connectionString), oracle => oracle
                .UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion21)
                .MigrationsAssembly(AdditionalMigrationAssemblies.Oracle)
                .CommandTimeout(timeoutSeconds)
                .ExecutionStrategy(dependencies => new OracleConnectionRecoveryStrategy(dependencies)));
        }
        else if (ConfiguredProviderNames.Normalize(provider) == ConfiguredProviderNames.SQLite) {
            options.UseSqlite(NormalizeSqliteConnectionString(connectionString, baseDirectory), sqlite => sqlite
                .MigrationsAssembly(AdditionalMigrationAssemblies.SQLite)
                .CommandTimeout(timeoutSeconds));
        }
        else throw new InvalidOperationException($"新增提供器选项不支持 {provider}。");
    }

    /// <summary>只规范化文件路径，不在配置解析或只读路由探测阶段创建目录和文件。</summary>
    public static string NormalizeSqliteConnectionString(string connectionString, string? baseDirectory = null) {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:" || builder.Mode == SqliteOpenMode.Memory)
            throw new InvalidOperationException("SQLite 业务数据库必须使用持久化文件，Data Source 不能为空或使用内存数据库。");
        builder.DataSource = Path.GetFullPath(builder.DataSource, Path.GetFullPath(baseDirectory ?? Directory.GetCurrentDirectory()));
        if (!builder.ContainsKey("Default Timeout")) builder.DefaultTimeout = 30;
        return builder.ConnectionString;
    }
}
