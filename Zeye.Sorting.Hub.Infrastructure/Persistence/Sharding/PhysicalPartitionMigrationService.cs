using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>把提供器的 EF Core 模型升级同步到历史分表，不手写业务 SQL。</summary>
public sealed class PhysicalPartitionMigrationService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions, IDatabaseDialect dialect, IConfiguration configuration, IHostEnvironment? environment = null) {
    /// <summary>分表升级和失败审计。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>主库迁移后检查历史分表，已确认当前版本的表组直接跳过，避免启动逐列扫描。</summary>
    public async Task ExecuteAsync(CancellationToken token) {
        await using var db = await factory.CreateDbContextAsync(token);
        var versions = (await db.Database.GetAppliedMigrationsAsync(token)).ToArray();
        if (versions.Length == 0) return;
        var assembly = db.GetService<IMigrationsAssembly>();
        var currentModel = db.GetService<IDesignTimeModel>().Model;
        var knownVersions = await db.Set<PartitionSchemaVersion>().AsNoTracking().ToDictionaryAsync(v => v.Key, v => v.MigrationId, token);
        var groups = new[] {
            (Scope: "Parcel", Root: "Parcels", Tables: currentModel.GetRelationalModel().Tables.Where(t => t.Name == "Parcels" || t.Name.StartsWith("Parcel_", StringComparison.Ordinal)).Select(t => t.Name).ToHashSet(StringComparer.Ordinal)),
            (Scope: "Audit", Root: "WebRequestAuditLogs", Tables: new HashSet<string>(["WebRequestAuditLogs", "WebRequestAuditLogDetails"], StringComparer.Ordinal))
        };
        foreach (var group in groups) {
            var names = await ((IBatchShardingPhysicalTableProbe)dialect).ListPhysicalTablesByBaseNameAsync(db, currentModel.GetDefaultSchema(), group.Root, token);
            foreach (var name in names) {
                var suffix = name[(group.Root.Length + 1)..];
                ParcelPartitionStore.ValidateSuffix(suffix);
                var key = group.Scope + ":" + suffix;
                if (knownVersions.TryGetValue(key, out var known) && known == versions[^1]) continue;
                await using var target = await partitions.CreateContextAsync(suffix, token, audit: group.Scope == "Audit");
                // 与正常预建使用同一锁资源，多个实例不会交叉升级和建表。
                await using var coordinator = await ParcelPartitionDdlCoordinator.AcquireAsync(target, group.Scope == "Audit" ? "Audit." + suffix : suffix, token);
                try {
                    var version = await target.Set<PartitionSchemaVersion>().AsTracking().SingleOrDefaultAsync(v => v.Key == key, token);
                    if (version?.MigrationId == versions[^1]) continue;
                    var previous = version?.MigrationId ?? await InferVersionAsync(target, coordinator, group.Tables, suffix, versions, assembly, token);
                    if (!assembly.Migrations.TryGetValue(previous, out var migrationType)) throw new InvalidOperationException($"找不到历史分表模型版本 {previous}。");
                    var previousModel = target.GetService<IModelRuntimeInitializer>().Initialize(assembly.CreateMigration(migrationType, target.Database.ProviderName!).TargetModel, designTime: true);
                    var operations = target.GetService<IMigrationsModelDiffer>().GetDifferences(previousModel.GetRelationalModel(), currentModel.GetRelationalModel())
                        .Where(operation => PartitionMigrationOperationRebaser.TableName(operation) is { } table && group.Tables.Contains(table))
                        .Select(operation => PartitionMigrationOperationRebaser.Rebase(operation, suffix, group.Tables, currentModel.GetMaxIdentifierLength(), group.Scope == "Audit")).ToList();
                    var pending = new List<MigrationOperation>();
                    // 步骤1：补齐部分 DDL 的重试缺口，避免重复创建已有列和索引。
                    foreach (var operation in operations) {
                        var table = PartitionMigrationOperationRebaser.TableName(operation)!;
                        var columns = await coordinator.ReadColumnsAsync(table, currentModel.GetDefaultSchema(), token);
                        if (operation is CreateTableOperation && columns.Count > 0) continue;
                        if (operation is AddColumnOperation add && columns.Contains(add.Name)) continue;
                        if (operation is DropColumnOperation drop && !columns.Contains(drop.Name)) continue;
                        if (operation is CreateIndexOperation index && await coordinator.IndexExistsAsync(table, index.Schema, index.Name, token)) continue;
                        if (operation is DropIndexOperation oldIndex && !await coordinator.IndexExistsAsync(table, oldIndex.Schema, oldIndex.Name, token)) continue;
                        pending.Add(operation);
                    }
                    var model = target.GetService<IDesignTimeModel>().Model;
                    // 没有版本标记的旧表也补齐模型索引，列结构相同不会掩盖索引迁移。
                    var expectedIndexes = target.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel()).OfType<CreateIndexOperation>()
                        .Where(index => group.Tables.Any(table => index.Table == table + "_" + suffix)).ToArray();
                    foreach (var tableIndexes in expectedIndexes.GroupBy(index => index.Table)) {
                        var missing = await ((IShardingPhysicalTableProbe)dialect).FindMissingIndexesAsync(target, model.GetDefaultSchema(), tableIndexes.Key, tableIndexes.Select(i => i.Name).ToArray(), token);
                        foreach (var index in tableIndexes.Where(index => missing.Contains(index.Name) && !pending.OfType<CreateIndexOperation>().Any(p => p.Name == index.Name))) pending.Add(index);
                    }
                    var commands = target.GetService<IMigrationsSqlGenerator>().Generate(pending, model);
                    var indexRollback = pending.OfType<CreateIndexOperation>().Reverse().Select(index => new DropIndexOperation { Name = index.Name, Table = index.Table, Schema = index.Schema }).Cast<MigrationOperation>().ToArray();
                    var rollback = target.GetService<IMigrationsSqlGenerator>().Generate(indexRollback, model);
                    Logger.Info("历史分表迁移审计：Key={Key}, From={From}, To={To}, DDL={DDL}, IndexRollbackDDL={RollbackDDL}, SchemaRollbackRequiresBackup={RequiresBackup}",
                        key, previous, versions[^1], string.Join(Environment.NewLine, commands.Select(c => c.CommandText)),
                        string.Join(Environment.NewLine, rollback.Select(c => c.CommandText)), pending.Any(operation => operation is not CreateIndexOperation));
                    var dangerous = MigrationSafetyEvaluator.EvaluateDangerousOperations(string.Join(Environment.NewLine, commands.Select(c => c.CommandText)));
                    if (commands.Count > 0 && AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:MigrationGovernance:DryRun", true))
                        throw new InvalidOperationException("历史分表迁移处于预演模式，已记录 DDL 但禁止更新结构或版本。");
                    if (dangerous.Count > 0 && environment?.IsProduction() == true && AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:MigrationGovernance:BlockDangerousMigrationInProduction", true))
                        throw new InvalidOperationException("历史分表检测到危险迁移，生产守卫已阻断：" + string.Join(" | ", dangerous));
                    if (commands.Count > 0 && (!AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:AllowTableCreation", false) || AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:DryRun", true)))
                        throw new InvalidOperationException("历史分表需要升级，建表隔离器禁止执行或处于预演模式。");
                    // 步骤2：EF 执行器处理事务与 SQLite 重建，成功后持久化版本。
                    await target.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, target.GetService<IRelationalConnection>(), token);
                    await SaveVersionAsync(target, key, versions[^1], token);
                }
                catch (Exception exception) { Logger.Error(exception, "历史物理分表迁移失败，Scope={Scope}, Suffix={Suffix}", group.Scope, suffix); throw; }
            }
        }
    }

    /// <summary>旧版无版本目录时，按真实列集合匹配历史模型；无法匹配则保留原表并阻断升级。</summary>
    private static async Task<string> InferVersionAsync(SortingHubDbContext db, ParcelPartitionDdlCoordinator coordinator, System.Collections.Generic.HashSet<string> tables, string suffix, string[] versions, IMigrationsAssembly assembly, CancellationToken token) {
        var actual = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var table in tables) actual[table] = await coordinator.ReadColumnsAsync(table + "_" + suffix, db.Model.GetDefaultSchema(), token);
        foreach (var id in versions.Reverse()) {
            var model = db.GetService<IModelRuntimeInitializer>().Initialize(assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!).TargetModel, designTime: true);
            var expected = model.GetRelationalModel().Tables.Where(table => tables.Contains(table.Name)).ToArray();
            if (expected.Length > 0 && expected.All(table => actual[table.Name].SetEquals(table.Columns.Select(column => column.Name))) && actual.All(pair => pair.Value.Count == 0 || expected.Any(table => table.Name == pair.Key))) return id;
        }
        throw new InvalidOperationException($"无法将旧分表 {suffix} 与历史 Code First 模型匹配，禁止推测覆盖结构。");
    }

    /// <summary>在建表或迁移成功后持久化版本，调用方持有该周期的跨进程锁。</summary>
    internal static async Task SaveVersionAsync(SortingHubDbContext db, string key, string migrationId, CancellationToken token) {
        var version = await db.Set<PartitionSchemaVersion>().AsTracking().SingleOrDefaultAsync(v => v.Key == key, token);
        if (version is null) { version = new PartitionSchemaVersion { Key = key }; db.Add(version); }
        version.MigrationId = migrationId; version.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(token);
    }
}
