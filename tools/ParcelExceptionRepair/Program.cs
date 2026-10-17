using System.Globalization;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Targets;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace ParcelExceptionRepair;

/// <summary>SQLite 现场异常修复入口，默认预览；修改及回滚必须显式指定执行开关和停机确认。</summary>
internal static class Program {
    /// <summary>审计日志写入本次输出目录并按十兆轮转。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>加载指定部署目录的真实配置，只修复已有数据库，绝不自动建库或建表。</summary>
    private static async Task<int> Main(string[] args) {
        var output = Path.GetFullPath(Value(args, "--output") ?? Path.Combine("exception-repair", DateTime.Now.ToString("yyyyMMdd-HHmmss-ffff", CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(output);
        var logging = new LoggingConfiguration();
        logging.AddRuleForAllLevels(new FileTarget("audit") { FileName = Path.Combine(output, "repair.log"),
            ArchiveAboveSize = 10485760, ArchiveEvery = FileArchivePeriod.Day, ArchiveSuffixFormat = "_{1:yyyyMMdd}_{0:00}" });
        LogManager.Configuration = logging;
        try {
            var root = Path.GetFullPath(Value(args, "--config-root") ?? throw new ArgumentException("必须提供 --config-root 部署目录。"));
            var environmentRoot = Environment.GetEnvironmentVariable("ZEYE_HUB_CONFIG_ROOT");
            if (environmentRoot is not null && !Path.GetFullPath(environmentRoot).Equals(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--config-root 与 ZEYE_HUB_CONFIG_ROOT 不一致；请明确同一个部署目录后重试。");
            var apply = args.Contains("--apply");
            var rollback = Value(args, "--rollback");
            if ((apply || rollback is not null) && !args.Contains("--confirm-stopped"))
                throw new ArgumentException("执行修改前必须停止 Sorting.Hub，并提供 --confirm-stopped；不带 --apply 时仅预览。");
            var values = ConfigurationDocument.Flatten(ConfigurationReadOnlyLoader.Load(root));
            if (!string.Equals(values.GetValueOrDefault("Persistence:Provider"), "SQLite", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("此现场修复工具只支持 SQLite，未对其他数据库执行操作。");
            var connection = new SqliteConnectionStringBuilder(values.GetValueOrDefault("ConnectionStrings:SQLite")
                ?? throw new InvalidOperationException("缺少 SQLite 连接配置。"));
            connection.DataSource = Path.GetFullPath(connection.DataSource, root);
            if (!File.Exists(connection.DataSource)) throw new FileNotFoundException("配置的业务数据库不存在，拒绝创建空库。", connection.DataSource);
            connection.Mode = apply || rollback is not null ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly;
            connection.Pooling = false;
            var factory = new PooledDbContextFactory<SortingHubDbContext>(new DbContextOptionsBuilder<SortingHubDbContext>().UseSqlite(connection.ToString()).Options);
            using var partitions = new ParcelPartitionStore(factory, new ConfigurationBuilder().AddInMemoryCollection(values).Build());
            var rules = rollback is null ? await ReadRulesAsync(root, values, factory) : [];
            var runner = new ExceptionRepairRunner(partitions, connection.DataSource, rules);
            if (rollback is not null) {
                foreach (var line in File.ReadLines(rollback)) {
                    var entry = JsonSerializer.Deserialize<ExceptionRepairEntry>(line) ?? throw new JsonException("修复审计行为空。");
                    await runner.UpdateAsync(entry, true, default);
                }
                Console.WriteLine("回滚完成；仅恢复审计记录中仍符合修复后状态的字段。");
            } else {
                var from = DateTime.ParseExact(Value(args, "--from") ?? throw new ArgumentException("缺少 --from yyyy-MM-dd。"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var to = DateTime.ParseExact(Value(args, "--to") ?? throw new ArgumentException("缺少 --to yyyy-MM-dd。"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var auditPath = Path.Combine(output, apply ? "applied.jsonl" : "preview.jsonl");
                await using var audit = new StreamWriter(new FileStream(auditPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
                var count = await runner.RepairAsync(from, to.AddDays(1), apply, audit, default);
                Console.WriteLine($"{(apply ? "已修复" : "预览待修复")} {count} 票；审计文件：{auditPath}");
            }
            return 0;
        } catch (Exception exception) {
            Logger.Error(exception, "历史异常修复停止。");
            Console.Error.WriteLine(exception.Message);
            return 1;
        } finally { LogManager.Shutdown(); }
    }

    /// <summary>读取配置快照中的异常与包裹规则，旧版本兼容关系库管理文档。</summary>
    private static async Task<IReadOnlyList<ClassificationRule>> ReadRulesAsync(string root, Dictionary<string, string?> values, IDbContextFactory<SortingHubDbContext> factory) {
        var path = Path.GetFullPath(Environment.GetEnvironmentVariable("ConfigurationStorage__LiteDbPath")
            ?? values.GetValueOrDefault("ConfigurationStorage:LiteDbPath") ?? "data/configuration/settings.db", root);
        var result = new List<ClassificationRule>();
        using var lite = File.Exists(path) ? new LiteDatabase(new ConnectionString { Filename = path, Connection = ConnectionType.Shared, ReadOnly = true }) : null;
        await using var db = await factory.CreateDbContextAsync();
        foreach (var key in new[] { "rules-exception", "rules-parcel" }) {
            var stored = lite?.GetCollection<BsonDocument>("configuration_documents").FindById(key);
            var json = stored is null ? (await db.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(row => row.Key == key))?.Json : stored["json"].AsString;
            result.AddRange(json is null ? key == "rules-exception" ? ClassificationRuleDefaults.Create() : []
                : JsonSerializer.Deserialize<ClassificationRule[]>(json, JsonSerializerOptions.Web) ?? throw new JsonException("分类规则无效。"));
        }
        return result;
    }

    /// <summary>读取具名参数，拒绝缺失值，布尔开关单独处理。</summary>
    private static string? Value(string[] args, string name) {
        var index = Array.IndexOf(args, name);
        if (index < 0) return null;
        return index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? args[index + 1] : throw new ArgumentException($"参数 {name} 缺少值。");
    }
}
