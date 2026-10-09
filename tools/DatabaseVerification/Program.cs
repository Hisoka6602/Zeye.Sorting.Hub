using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>仅在独立 Docker 验收环境执行的 EF 验证入口，禁止连接现有部署。</summary>
internal static class Program {
    /// <summary>失败落盘日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>检查迁移、分表、调优和 Fusion 原始事实，所有业务查询使用 EF。</summary>
    private static async Task<int> Main(string[] args) {
        try {
            if (Environment.GetEnvironmentVariable("ZEYE_DATABASE_VERIFICATION") != "1" || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "DatabaseVerification")
                throw new InvalidOperationException("验收工具只接受显式启用的 DatabaseVerification 环境。");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(ConfigurationDocument.Flatten(ConfigurationReadOnlyLoader.Load(Directory.GetCurrentDirectory(), "DatabaseVerification")))
                .AddEnvironmentVariables().Build();
            var provider = ConfiguredProviderNames.Normalize(configuration["Persistence:Provider"]);
            var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSortingHubPersistence(configuration);
            var queryTrace = args.Contains("--trace-queries") ? new QueryCommandTraceInterceptor() : null;
            if (queryTrace is not null) services.AddDbContextFactory<SortingHubDbContext>(options => options.AddInterceptors(queryTrace));
            services.AddSingleton<PartitionMaintenanceService>();
            await using var container = services.BuildServiceProvider();
            var dialect = container.GetRequiredService<IDatabaseDialect>();
            var databaseName = dialect.ExtractDatabaseName(configuration.GetConnectionString(provider)!);
            if (provider == "Oracle" ? databaseName != "ZEYE_MATRIX_FIXED" : provider == "SQLite" ? !databaseName.Replace('\\', '/').EndsWith("data/business/nested/sorting-hub.db", StringComparison.Ordinal) : databaseName != "zeye_matrix")
                throw new InvalidOperationException("验收工具只允许专属测试数据库或文件。");
            var factory = container.GetRequiredService<IDbContextFactory<SortingHubDbContext>>();
            var partitions = container.GetRequiredService<ParcelPartitionStore>();
            await using var db = await factory.CreateDbContextAsync();
            if (args.Contains("--probe-connection")) {
                // 专属环境与库名校验后仅打开连接，错误凭据及只读缺失文件不触发维护或迁移。
                Console.WriteLine(JsonSerializer.Serialize(new { provider, executionStrategy = db.Database.CreateExecutionStrategy().GetType().Name }));
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { provider, connectionOpened = true }));
                return 0;
            }
            if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Code First 模型或迁移历史不一致。");
            if (args.Contains("--seed-cleanup-case")) {
                Console.WriteLine(JsonSerializer.Serialize(await CleanupFixtureScenario.SeedAsync(factory, partitions)));
                return 0;
            }
            if (args.Contains("--protocol-faults")) {
                Console.WriteLine(JsonSerializer.Serialize(await FusionProtocolFaultScenario.ExecuteAsync(factory)));
                return 0;
            }
            var period = partitions.Resolve(DateTime.Now);
            if (args.Contains("--hold-write-lock")) {
                var seconds = args.SkipWhile(argument => argument != "--hold-write-lock").Skip(1).FirstOrDefault()
                    ?? throw new ArgumentException("缺少 SQLite 写锁持有时长。");
                await SqliteWriteLockScenario.ExecuteAsync(db, period.Suffix, seconds);
                return 0;
            }
            await using var shard = await partitions.CreateContextAsync(period.Suffix, CancellationToken.None);
            var index = shard.GetService<IMigrationsModelDiffer>().GetDifferences(null, shard.GetService<IDesignTimeModel>().Model.GetRelationalModel())
                .OfType<CreateIndexOperation>().First(index => index.Table == "Parcels_" + period.Suffix);
            if (args.Contains("--assert-startup-upgrade")) {
                // 只读检查必须在任何维护或建表动作之前执行，明确验证宿主启动迁移的结果。
                var missing = await ((IShardingPhysicalTableProbe)dialect).FindMissingIndexesAsync(shard, index.Schema, index.Table, [index.Name], CancellationToken.None);
                var version = await db.Set<PartitionSchemaVersion>().AsNoTracking().SingleOrDefaultAsync(row => row.Key == "Parcel:" + period.Suffix);
                if (missing.Count > 0 || version?.MigrationId != (await db.Database.GetAppliedMigrationsAsync()).Last())
                    throw new InvalidOperationException("启动升级检查失败：分表索引或模型版本尚未恢复。");
                var durationIndexesVerified = 0;
                var dwsWindowIndexesVerified = 0;
                var catalog = await partitions.GetReadCatalogAsync(CancellationToken.None);
                foreach (var suffix in catalog.Suffixes) {
                    await using var physical = await partitions.CreateContextAsync(suffix, CancellationToken.None);
                    var operations = physical.GetService<IMigrationsModelDiffer>().GetDifferences(null, physical.GetService<IDesignTimeModel>().Model.GetRelationalModel())
                        .OfType<CreateIndexOperation>().ToArray();
                    var durationIndex = operations.Single(operation => operation.Columns.SequenceEqual(new[] {
                            "SourceInstanceId", "Stage", "PartitionTime", "ParcelId", "OccurredAt", "SourceRunId", "SourceParcelId", "RecordId", "IsSuccess", "HasReliableTimestamp" }));
                    var missingDuration = await ((IShardingPhysicalTableProbe)dialect).FindMissingIndexesAsync(physical,
                        durationIndex.Schema, durationIndex.Table, [durationIndex.Name], CancellationToken.None);
                    if (missingDuration.Count > 0) throw new InvalidOperationException("启动升级未创建来源阶段覆盖索引：" + durationIndex.Name);
                    durationIndexesVerified++;
                    var dwsIndex = operations.Single(operation => operation.Columns.SequenceEqual(new[] { "PartitionTime", "Stage", "IsSuccess" }));
                    var missingDws = await ((IShardingPhysicalTableProbe)dialect).FindMissingIndexesAsync(physical,
                        dwsIndex.Schema, dwsIndex.Table, [dwsIndex.Name], CancellationToken.None);
                    if (missingDws.Count > 0) throw new InvalidOperationException("启动升级未创建 DWS 时间边界覆盖索引：" + dwsIndex.Name);
                    dwsWindowIndexesVerified++;
                }
                Console.WriteLine(JsonSerializer.Serialize(new { provider, period.Suffix, startupUpgradeVerified = true, index.Name, version.MigrationId, durationIndexesVerified, dwsWindowIndexesVerified }));
                return 0;
            }
            string? concurrentPartition = null;
            if (args.Contains("--race-partition")) {
                var future = partitions.Resolve(DateTime.Now.AddMonths(2));
                await partitions.EnsureCreatedAsync(future, CancellationToken.None, verifyExisting: true);
                concurrentPartition = future.Suffix;
            }
            await container.GetRequiredService<PartitionMaintenanceService>().ExecuteAsync(CancellationToken.None);
            await container.GetRequiredService<AuditPartitionMaintenanceService>().ExecuteAsync(1, CancellationToken.None);
            if (args.Contains("--query-performance")) {
                var countText = args.SkipWhile(argument => argument != "--query-performance").Skip(1).FirstOrDefault();
                var count = int.TryParse(countText, out var parsed) ? parsed : 10000;
                var result = await QueryPerformanceScenario.ExecuteAsync(container, count);
                if (queryTrace is not null) await queryTrace.WriteAsync();
                Console.WriteLine(JsonSerializer.Serialize(result));
                return 0;
            }
            if (args.Contains("--prepare-legacy-index")) {
                // 仅移除专属测试环境的一个可重建索引，模拟无模型版本目录的旧分表。
                var commands = shard.GetService<IMigrationsSqlGenerator>().Generate([new DropIndexOperation { Table = index.Table, Schema = index.Schema, Name = index.Name }]);
                foreach (var command in commands) await shard.Database.ExecuteSqlRawAsync(command.CommandText);
                await db.Set<PartitionSchemaVersion>().Where(version => version.Key == "Parcel:" + period.Suffix).ExecuteDeleteAsync();
                Console.WriteLine(JsonSerializer.Serialize(new { provider, legacyIndexPrepared = index.Name, period.Suffix }));
                return 0;
            }
            var missingIndexes = await ((IShardingPhysicalTableProbe)dialect).FindMissingIndexesAsync(shard, index.Schema, index.Table, [index.Name], CancellationToken.None);
            if (missingIndexes.Count > 0) throw new InvalidOperationException("旧分表索引尚未按 Code First 模型恢复：" + index.Name);
            var tuning = dialect.BuildAutomaticTuningSql(null, "Parcels_" + period.Suffix, ["SourceInstanceId", "SourceRunId"]);
            foreach (var sql in tuning) {
                try { await shard.Database.ExecuteSqlRawAsync(sql); }
                catch (Exception exception) when (dialect.ShouldIgnoreAutoTuningException(exception)) { Logger.Warn(exception, "验收重复索引按幂等规则跳过。"); }
            }
            // 接收确认只表示原始事实已提交；等待后台业务投影后再做最终对账。
            // 进程强制中断后允许两分钟认领租约到期，仍以全部完成作为有限等待的验收条件。
            for (var attempt = 0; attempt < 360 && await db.Set<FusionFactReceipt>().AnyAsync(fact => fact.ProjectionState != "complete"); attempt++)
                await Task.Delay(500);
            var facts = await db.Set<FusionFactReceipt>().AsNoTracking().ToListAsync();
            if (facts.Any(fact => fact.BodySha256 != Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fact.BodyJson))))) throw new InvalidOperationException("Fusion 原始事实摘要不一致。");
            var pending = facts.Count(fact => fact.ProjectionState != "complete");
            if (pending != 0) throw new InvalidOperationException($"Fusion 尚有 {pending} 条未完成投影。");
            var source = args.SkipWhile(argument => argument != "--source").Skip(1).FirstOrDefault();
            var evidencePath = source is null ? null : await FusionEvidenceExporter.WriteAsync(db, shard, source, CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(new { provider, migrations = await db.Database.GetAppliedMigrationsAsync(), period.Suffix,
                parcelCount = await shard.Set<Parcel>().CountAsync(), processingRecords = await shard.Set<ParcelProcessingRecord>().CountAsync(),
                facts = facts.Count, pending, images = await db.Set<FusionImageUpload>().CountAsync(image => image.IsStored),
                partitionVersions = await db.Set<PartitionSchemaVersion>().CountAsync(), tuningActions = tuning.Count, evidencePath, concurrentPartition }));
            return 0;
        }
        catch (Exception exception) {
            if (exception is Oracle.ManagedDataAccess.Client.OracleException oracle)
                Console.Error.WriteLine(JsonSerializer.Serialize(new { oracleErrorNumber = oracle.Number }));
            Logger.Error(exception, "独立数据库验收失败。"); Console.Error.WriteLine(exception); return 1;
        }
    }
}
