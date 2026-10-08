using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>用当前模型成对预建审计日表，补齐索引，保留已有表和数据。</summary>
public sealed class AuditPartitionMaintenanceService(IDbContextFactory<SortingHubDbContext> factory, IConfiguration configuration) {
    /// <summary>有界进程内建表锁，跨实例协调复用数据库会话锁。</summary>
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    /// <summary>实际建表操作的审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>只允许预建模型定义的审计热表和详情表。</summary>
    private static readonly HashSet<string> BaseTables = ["WebRequestAuditLogs", "WebRequestAuditLogDetails"];

    /// <summary>包含当前日、配置窗口及至少下一日，跨月或跨年保持完整日期。</summary>
    public static IReadOnlyList<DateTime> Plan(DateTime startAtLocal, int aheadHours) {
        if (aheadHours is < 1 or > 168) throw new ArgumentOutOfRangeException(nameof(aheadHours));
        var end = startAtLocal.AddHours(aheadHours).Date;
        if (end == startAtLocal.Date) end = end.AddDays(1);
        var days = new List<DateTime>();
        for (var day = startAtLocal.Date; day <= end; day = day.AddDays(1)) days.Add(day);
        return days;
    }

    /// <summary>仅在显式允许建表且关闭预演时执行，不接受外部 SQL 或表名。</summary>
    public async Task ExecuteAsync(int aheadHours, CancellationToken ct) {
        foreach (var day in Plan(DateTime.Now, aheadHours)) await EnsureCreatedAsync(day, ct);
    }

    /// <summary>会话锁内补齐日表和索引；已有结构不同则报错，禁止覆盖。</summary>
    public async Task EnsureCreatedAsync(DateTime day, CancellationToken ct) {
        if (!AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:AllowTableCreation", false) || AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:DryRun", true))
            throw new InvalidOperationException("审计日表预建需要显式允许物理建表并关闭建表预演。");
        var suffix = day.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var gate = Gates[(int)((uint)StringComparer.Ordinal.GetHashCode(suffix) % (uint)Gates.Length)];
        await gate.WaitAsync(ct);
        try {
            await using var db = await factory.CreateDbContextAsync(ct);
            var model = db.GetService<IDesignTimeModel>().Model;
            var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel())
                .Where(op => op is CreateTableOperation table && BaseTables.Contains(table.Name) || op is CreateIndexOperation index && BaseTables.Contains(index.Table)).ToList();
            foreach (var op in operations) {
                if (op is CreateTableOperation table) {
                    table.Name += "_" + suffix;
                    foreach (var column in table.Columns) column.Table = table.Name;
                    if (table.PrimaryKey is not null) { table.PrimaryKey.Table = table.Name; table.PrimaryKey.Name = PhysicalName(table.PrimaryKey.Name, suffix); }
                    foreach (var unique in table.UniqueConstraints) { unique.Table = table.Name; unique.Name = PhysicalName(unique.Name, suffix); }
                    foreach (var key in table.ForeignKeys) {
                        key.Table = table.Name; key.Name = PhysicalName(key.Name, suffix);
                        if (BaseTables.Contains(key.PrincipalTable)) key.PrincipalTable += "_" + suffix;
                    }
                }
                else if (op is CreateIndexOperation index) { index.Table += "_" + suffix; index.Name = PhysicalName(index.Name, suffix); }
            }
            await using var coordinator = await ParcelPartitionDdlCoordinator.AcquireAsync(db, "Audit." + suffix, ct);
            var pending = new List<MigrationOperation>();
            foreach (var op in operations) {
                if (op is CreateTableOperation table) {
                    var columns = await coordinator.ReadColumnsAsync(table.Name, table.Schema, ct);
                    if (columns.Count == 0) pending.Add(table);
                    else if (!columns.SetEquals(table.Columns.Select(x => x.Name))) throw new InvalidOperationException($"审计日表 {table.Name} 结构与模型不一致，请先迁移。");
                }
                else if (op is CreateIndexOperation index && !await coordinator.IndexExistsAsync(index.Table, index.Schema, index.Name, ct)) pending.Add(index);
            }
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(pending, model);
            if (commands.Count > 0) Logger.Info("审计日表建表审计：Suffix={Suffix}, DDL={DDL}", suffix, string.Join(Environment.NewLine, commands.Select(x => x.CommandText)));
            await db.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, db.GetService<IRelationalConnection>(), ct);
            var migration = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();
            if (migration is not null) await PhysicalPartitionMigrationService.SaveVersionAsync(db, "Audit:" + suffix, migration, ct);
        }
        finally { gate.Release(); }
    }

    /// <summary>索引及约束名在 SQLite 全库唯一，并满足 MySQL 的 64 字符限制。</summary>
    internal static string PhysicalName(string name, string suffix) => (name.Length <= 54 ? name : name[..45] + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8]) + "_" + suffix;
}
