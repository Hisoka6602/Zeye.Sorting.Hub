using System.Data;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>实际物理分表路由、目录查询与受隔离器保护的建表。</summary>
public sealed class ParcelPartitionStore : IDisposable {
    /// <summary>所有目录刷新任务停止后释放实例闸门；进程级建表闸门仍由进程共享。</summary>
    public void Dispose() => _catalogRefreshGate.Dispose();
    /// <summary>基础模型上下文工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>复用分表上下文配置，模型仍按实际后缀隔离，不在每次查询时重建选项。</summary>
    private DbContextOptions<SortingHubDbContext>? _partitionOptions;
    /// <summary>新包裹使用的配置粒度；运行时不读取配置文件。</summary>
    private readonly ParcelTimeShardingGranularity _granularity;
    /// <summary>是否显式允许创建物理分表，默认false。</summary>
    private readonly bool _allowCreation;
    /// <summary>是否仅预览DDL，默认true。</summary>
    private readonly bool _dryRun;
    /// <summary>每个实际分表只做一次建表就绪检查，维护复核仍显式访问数据库。</summary>
    private readonly ConcurrentDictionary<string, ParcelPartitionPeriod> _readyPartitions = new(StringComparer.Ordinal);
    /// <summary>仅冷启动与后台刷新使用的单飞读取闸门。</summary>
    private readonly SemaphoreSlim _catalogRefreshGate = new(1, 1);
    /// <summary>发布目录及本进程建表结果的短内存锁。</summary>
    private readonly object _catalogPublishGate = new();
    /// <summary>已登记周期及查询后缀的一致快照。</summary>
    private ParcelPartitionCatalogSnapshot? _catalog;
    /// <summary>本进程建表发布版本，阻止较早的数据库读取覆盖新建周期。</summary>
    private long _catalogRevision;
    /// <summary>有界进程内建表锁，生产数据库另用会话锁协调多个Hub实例。</summary>
    private static readonly SemaphoreSlim[] CreationGates = Enumerable.Range(0, 64).Select(static _ => new SemaphoreSlim(1, 1)).ToArray();
    /// <summary>仅允许系统生成的日期或ISO周后缀。</summary>
    private static readonly Regex SuffixPattern = new(@"^(\d{6}|\d{8}|\d{4}W\d{2})$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    /// <summary>物理分表DDL及异常审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>启动时读取并校验时间粒度和DDL隔离器配置。</summary>
    public ParcelPartitionStore(IDbContextFactory<SortingHubDbContext> factory, IConfiguration configuration) {
        _factory = factory;
        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        if (evaluation.ValidationErrors.Count > 0) throw new ArgumentException(string.Join(";", evaluation.ValidationErrors));
        var decision = evaluation.Decision;
        _granularity = decision.ThresholdReached && decision.ThresholdAction == ParcelVolumeThresholdAction.SwitchToPerDay
            ? ParcelTimeShardingGranularity.PerDay : decision.TimeGranularity;
        _allowCreation = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:AllowTableCreation", false);
        _dryRun = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, "Persistence:Sharding:WriteRouting:DryRun", true);
    }

    /// <summary>解析首次入库时间对应的周期。</summary>
    public ParcelPartitionPeriod Resolve(DateTime registeredAt) => ParcelPartitionPeriod.Resolve(registeredAt, _granularity);

    /// <summary>创建使用实际物理表模型的独立上下文。</summary>
    public async Task<SortingHubDbContext> CreateContextAsync(string suffix, CancellationToken cancellationToken, bool audit = false) {
        ValidateSuffix(suffix);
        cancellationToken.ThrowIfCancellationRequested();
        var options = Volatile.Read(ref _partitionOptions);
        if (options is null) {
            await using var template = await _factory.CreateDbContextAsync(cancellationToken);
            var configured = new DbContextOptionsBuilder<SortingHubDbContext>((DbContextOptions<SortingHubDbContext>)template.GetService<IDbContextOptions>())
                .ReplaceService<IModelCacheKeyFactory, ParcelPartitionModelCacheKeyFactory>().Options;
            options = Interlocked.CompareExchange(ref _partitionOptions, configured, null) ?? configured;
        }
        return new SortingHubDbContext(options) { ParcelPartitionSuffix = audit ? string.Empty : suffix, AuditPartitionSuffix = audit ? suffix : string.Empty };
    }

    /// <summary>校验目录后缀，拒绝将外部文本拼入SQL表名。</summary>
    public static void ValidateSuffix(string suffix) {
        if (suffix.Length > 0 && !SuffixPattern.IsMatch(suffix)) throw new ArgumentException("物理分表后缀非法。");
    }

    /// <summary>根据全局定位索引查询已有包裹所属分表；没有索引时兼容历史基础表。</summary>
    public async Task<string> LocateAsync(long id, CancellationToken cancellationToken) {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<ParcelLocation>().Where(x => x.Id == id).Select(x => x.Suffix).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    /// <summary>列出全部已登记物理分表及历史基础表，配置更改后仍可查询旧粒度数据。</summary>
    public async Task<IReadOnlyList<string>> GetReadSuffixesAsync(CancellationToken cancellationToken) {
        return (await GetReadCatalogAsync(cancellationToken)).Suffixes;
    }

    /// <summary>已预热的目录直接从内存返回；未启动托管服务的独立消费者仅在第一次访问时加载。</summary>
    public async Task<ParcelPartitionCatalogSnapshot> GetReadCatalogAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = Volatile.Read(ref _catalog);
        if (snapshot is not null) return snapshot;
        return await RefreshReadCatalogAsync(cancellationToken, onlyIfMissing: true);
    }

    /// <summary>后台同步其他 Hub 创建的周期，先完整读取再原子发布，不污染已成功加载的快照。</summary>
    public async Task<ParcelPartitionCatalogSnapshot> RefreshReadCatalogAsync(CancellationToken cancellationToken, bool onlyIfMissing = false) {
        await _catalogRefreshGate.WaitAsync(cancellationToken);
        try {
            var snapshot = Volatile.Read(ref _catalog);
            if (onlyIfMissing && snapshot is not null) return snapshot;
            var revision = Interlocked.Read(ref _catalogRevision);
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entries = await db.Set<ParcelPartitionCatalogEntry>().AsNoTracking().ToArrayAsync(cancellationToken);
            var periods = entries.Select(entry => {
                ValidateSuffix(entry.Suffix);
                return new ParcelPartitionPeriod { Suffix = entry.Suffix, Start = entry.Start, End = entry.End,
                    Granularity = entry.Suffix.Contains('W') ? ParcelTimeShardingGranularity.PerWeek
                        : entry.Suffix.Length == 8 ? ParcelTimeShardingGranularity.PerDay : ParcelTimeShardingGranularity.PerMonth };
            }).ToArray();
            lock (_catalogPublishGate) {
                if (revision != _catalogRevision)
                    periods = periods.Concat(_readyPartitions.Values).DistinctBy(period => period.Suffix).ToArray();
                snapshot = new(periods);
                Volatile.Write(ref _catalog, snapshot);
                var registered = periods.Select(period => period.Suffix).ToHashSet(StringComparer.Ordinal);
                foreach (var suffix in _readyPartitions.Keys)
                    if (!registered.Contains(suffix)) _readyPartitions.TryRemove(suffix, out _);
                foreach (var period in periods) _readyPartitions[period.Suffix] = period;
            }
            return snapshot;
        }
        catch (Exception exception) {
            Logger.Error(exception, "包裹分表目录快照刷新失败，保留已发布目录。");
            throw;
        }
        finally { _catalogRefreshGate.Release(); }
    }

    /// <summary>仅在目录确认或耐久提交成功后记录就绪，已加载的读目录立即包含新周期。</summary>
    private void PublishCreatedPeriod(ParcelPartitionPeriod period) {
        lock (_catalogPublishGate) {
            var newlyReady = _readyPartitions.TryAdd(period.Suffix, period);
            if (_catalog is { } current && !current.Periods.Any(entry => entry.Suffix == period.Suffix)) {
                Volatile.Write(ref _catalog, new(current.Periods.Append(period)));
                _catalogRevision++;
            }
            else if (newlyReady) _catalogRevision++;
        }
    }

    /// <summary>预建指定周期的所有聚合表，成功后登记目录；默认只审计并阻止执行。</summary>
    public async Task EnsureCreatedAsync(ParcelPartitionPeriod period, CancellationToken cancellationToken, bool verifyExisting = false) {
        ValidateSuffix(period.Suffix);
        if (!verifyExisting && _readyPartitions.ContainsKey(period.Suffix)) {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }
        var gate = CreationGates[(int)((uint)StringComparer.Ordinal.GetHashCode(period.Suffix) % (uint)CreationGates.Length)];
        await gate.WaitAsync(cancellationToken);
        try {
            if (!verifyExisting && _readyPartitions.ContainsKey(period.Suffix)) return;
            await using var db = await CreateContextAsync(period.Suffix, cancellationToken);
            if (!verifyExisting && await db.Set<ParcelPartitionCatalogEntry>().AnyAsync(x => x.Suffix == period.Suffix, cancellationToken)) {
                PublishCreatedPeriod(period);
                return;
            }
            // 步骤1：使用提供器自身的模型差异与DDL生成器，不复制MySQL和SQLServer实现。
            var model = db.GetService<IDesignTimeModel>().Model;
            var allOperations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
            var operations = allOperations.Where(operation => operation switch {
                CreateTableOperation table => IsPartitionTable(table.Name, period.Suffix),
                CreateIndexOperation index => IsPartitionTable(index.Table, period.Suffix),
                _ => false
            }).ToList();
            await using var coordinator = await ParcelPartitionDdlCoordinator.AcquireAsync(db, period.Suffix, cancellationToken);
            var hasCatalog = await db.Set<ParcelPartitionCatalogEntry>().AnyAsync(x => x.Suffix == period.Suffix, cancellationToken);
            if (hasCatalog && !verifyExisting) { PublishCreatedPeriod(period); return; }
            // 步骤2：锁内复核实际表，容许同一模型的部分DDL重试；旧结构必须先迁移。
            var pending = new List<MigrationOperation>();
            foreach (var operation in operations) {
                if (operation is CreateTableOperation table) {
                    var columns = await coordinator.ReadColumnsAsync(table.Name, table.Schema, cancellationToken);
                    if (columns.Count == 0) pending.Add(operation);
                    else if (!columns.SetEquals(table.Columns.Select(x => x.Name))) throw new InvalidOperationException($"分表{table.Name}结构与当前模型不一致，请先执行结构迁移。");
                }
                else if (operation is CreateIndexOperation index && !await coordinator.IndexExistsAsync(index.Table, index.Schema, index.Name, cancellationToken)) pending.Add(operation);
            }
            if (pending.Count == 0 && hasCatalog) { PublishCreatedPeriod(period); return; }
            var generator = db.GetService<IMigrationsSqlGenerator>();
            var commands = generator.Generate(pending, model);
            // 步骤3：索引升级及对应回滚语句先进入落盘审计，预演和关闭开关始终不执行DDL。
            var rollbackIndexes = pending.OfType<CreateIndexOperation>().Reverse().Select(index => new DropIndexOperation {
                Name = index.Name, Table = index.Table, Schema = index.Schema
            }).Cast<MigrationOperation>().ToArray();
            var rollback = generator.Generate(rollbackIndexes, model);
            Logger.Info("包裹分表维护审计：Suffix={Suffix}, AllowTableCreation={Allowed}, DryRun={DryRun}, CommandCount={Count}, DDL={DDL}, IndexRollbackDDL={RollbackDDL}",
                period.Suffix, _allowCreation, _dryRun, commands.Count,
                string.Join(Environment.NewLine, commands.Select(x => x.CommandText)), string.Join(Environment.NewLine, rollback.Select(x => x.CommandText)));
            if (!_allowCreation || _dryRun) throw new InvalidOperationException("目标分表或索引尚未预建。请核查DDL审计，显式启用Persistence:Sharding:WriteRouting:AllowTableCreation并关闭DryRun，或提前执行预建。");
            // 步骤4：MySQL的DDL独立提交，业务数据事务在建表之后开始。
            await db.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, db.GetService<IRelationalConnection>(), cancellationToken);
            if (!hasCatalog) {
                db.Add(new ParcelPartitionCatalogEntry { Suffix = period.Suffix, Start = period.Start, End = period.End, CreatedTime = DateTime.Now });
                await db.SaveChangesAsync(cancellationToken);
            }
            var migration = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).LastOrDefault();
            if (migration is not null) await PhysicalPartitionMigrationService.SaveVersionAsync(db, "Parcel:" + period.Suffix, migration, cancellationToken);
            PublishCreatedPeriod(period);
        }
        catch (Exception ex) {
            _readyPartitions.TryRemove(period.Suffix, out _);
            Logger.Error(ex, "包裹物理分表预建失败，Suffix={Suffix}", period.Suffix);
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>判断DDL目标是否为当前周期的包裹聚合表。</summary>
    private static bool IsPartitionTable(string name, string suffix) => (name.StartsWith("Parcels_", StringComparison.Ordinal) || name.StartsWith("Parcel_", StringComparison.Ordinal)) && name.EndsWith("_" + suffix, StringComparison.Ordinal);
}
