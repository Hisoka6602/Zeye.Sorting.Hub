using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>处理记录追加、全局去重与包裹快照的事务仓储。</summary>
public sealed partial class ParcelProcessingRepository : IParcelProcessingRepository {
    /// <summary>基础表上下文工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>实际分表路由与预建。</summary>
    private readonly ParcelPartitionStore _partitions;
    /// <summary>已提交的内存分类配置，处理事务不读取管理文档表。</summary>
    private readonly ClassificationRuleSnapshotCache _rules;
    /// <summary>有界进程内锁，数据库串行化事务提供跨进程保护。</summary>
    private static readonly SemaphoreSlim[] WriteGates = Enumerable.Range(0, 256).Select(static _ => new SemaphoreSlim(1, 1)).ToArray();
    /// <summary>持久化失败与冲突审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>组装处理记录仓储。</summary>
    public ParcelProcessingRepository(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) {
        _factory = factory; _partitions = partitions;
        _rules = ClassificationRuleSnapshotCache.For(factory);
    }

    /// <summary>重试整个事务，原子保存凭据、记录、定位索引与包裹快照。</summary>
    public async Task<RepositoryResult<ParcelProcessingWriteResult>> AppendAsync(ParcelProcessingRecord record, CancellationToken cancellationToken) {
        var sourceKey = HashIdentity(record.SourceInstanceId, record.SourceRunId, record.SourceParcelId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? record.RecordId);
        var recordKey = HashIdentity(record.SourceInstanceId, record.SourceRunId, record.RecordId);
        var gate = WriteGates[Convert.ToByte(sourceKey[..2], 16)];
        await gate.WaitAsync(cancellationToken);
        try {
            record.Validate();
            var rules = await _rules.GetAsync(cancellationToken);
            await using var template = await _factory.CreateDbContextAsync(cancellationToken);
            var strategy = template.Database.CreateExecutionStrategy();
            var providerName = template.Database.ProviderName;
            var useRowLock = providerName is DbProviderNames.SqlServer or DbProviderNames.Oracle;
            /// <summary>执行一次完整的凭据检查与原子写入，供事务策略和唯一键竞争重试。</summary>
            async Task<RepositoryResult<ParcelProcessingWriteResult>> AppendOnceAsync() {
                // 步骤1：全局凭据优先检查，重试跨周期仍命中首次写入的物理表。
                await using var lookup = await _factory.CreateDbContextAsync(cancellationToken);
                var existingReceipt = await lookup.Set<ParcelProcessingReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == recordKey, cancellationToken);
                if (existingReceipt is not null) return CheckReceipt(existingReceipt, record.PayloadHash);
                var location = record.SourceParcelId.HasValue
                    ? await lookup.Set<ParcelLocation>().AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken)
                    : null;
                var period = _partitions.Resolve(record.RecordedAt);
                var suffix = location?.Suffix ?? period.Suffix;
                if (location is null) await _partitions.EnsureCreatedAsync(period, cancellationToken);
                var newParcelId = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(sourceKey)) & long.MaxValue;
                if (newParcelId == 0) newParcelId = 1;
                // 兼容基础表的碰撞检查在占用分表事务连接前完成，避免满池并行任务互等第二条连接。
                var baseIdCollision = location is null && record.SourceParcelId.HasValue
                    && await lookup.Set<Parcel>().AnyAsync(x => x.Id == newParcelId, cancellationToken);
                await using var db = await _partitions.CreateContextAsync(suffix, cancellationToken);
                // Oracle 串行化事务会因其他包裹修改同一数据块而失败；SQL Server 缺失键范围锁会死锁。
                // 已存在来源先通过 EF 更新取得行锁；新来源靠唯一约束竞争后重试整个事务。
                await using var transaction = await db.Database.BeginTransactionAsync(
                    useRowLock ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken);
                if (useRowLock && record.SourceParcelId.HasValue)
                    await LockParcelLocationAsync(db, sourceKey, cancellationToken);
                // 步骤2：事务内复核身份；并发冲突交由整个事务重试，不吞掉已变更的消息。
                existingReceipt = await db.Set<ParcelProcessingReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == recordKey, cancellationToken);
                if (existingReceipt is not null) return CheckReceipt(existingReceipt, record.PayloadHash);
                Parcel? parcel = null;
                if (record.SourceParcelId.HasValue) {
                    location = await db.Set<ParcelLocation>().SingleOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken);
                    if (location is null) {
                        var id = newParcelId;
                        // 步骤3：碰撞显式失败，禁止覆盖其他来源或历史基础表的同编号包裹。
                        if (baseIdCollision || await db.Set<ParcelLocation>().AnyAsync(x => x.Id == id, cancellationToken))
                            return RepositoryResult.Fail<ParcelProcessingWriteResult>("包裹来源身份与现有中心编号冲突。", "ParcelSourceConflict");
                        parcel = Parcel.CreateDetected(id, record, record.RecordedAt);
                        location = new ParcelLocation { Id = id, SourceKey = sourceKey, Suffix = suffix, CreatedTime = record.RecordedAt };
                        db.Add(location); db.Add(parcel);
                    }
                    else {
                        if (location.Suffix != suffix) throw new InvalidOperationException("并发写入已固定其他分表，请使用相同消息重试。");
                        parcel = await db.Set<Parcel>().AsTracking().SingleAsync(x => x.Id == location.Id, cancellationToken);
                        // 同一计数会话中的来源编号只允许一次检测；新RecordId不能把计数重置误并入旧包裹。
                        if (record.Stage == ParcelProcessingStage.Detected
                            && await db.Set<ParcelProcessingRecord>().AsNoTracking().AnyAsync(
                                x => x.ParcelId == parcel.Id && x.Stage == ParcelProcessingStage.Detected,
                                cancellationToken)) {
                            return RepositoryResult.Fail<ParcelProcessingWriteResult>(
                                "同一来源会话和包裹编号已存在检测记录；重试必须复用RecordId，设备计数重置必须更换SourceRunId。",
                                "ParcelSourceConflict");
                        }
                    }
                }
                var storedRecord = record with { Key = recordKey, ParcelId = parcel?.Id, PartitionTime = location?.CreatedTime ?? record.RecordedAt };
                db.Add(storedRecord);
                db.Add(new ParcelProcessingReceipt { Key = recordKey, PayloadHash = record.PayloadHash, ParcelId = parcel?.Id, Suffix = suffix, RecordedAt = record.RecordedAt });
                if (parcel is not null) {
                    var history = await db.Set<ParcelProcessingRecord>().AsNoTracking().Where(x => x.ParcelId == parcel.Id).ToListAsync(cancellationToken);
                    history.Add(storedRecord);
                    parcel.ApplyProcessingRecords(history, rules);
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return RepositoryResult.Success<ParcelProcessingWriteResult>(new() { ParcelId = parcel?.Id, PartitionSuffix = suffix });
            }
            for (var conflictAttempt = 0; ; conflictAttempt++) {
                try { return await strategy.ExecuteAsync(AppendOnceAsync); }
                catch (Exception ex) when (IsRetryableWriteConflict(providerName, ex) && conflictAttempt < 8) {
                    Logger.Warn(ex, "处理记录并发竞争，重试完整事务，RecordId={RecordId}, Attempt={Attempt}", record.RecordId, conflictAttempt + 1);
                    await Task.Delay(5 * (conflictAttempt + 1), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException ex) { Logger.Warn(ex, "处理记录写入已取消，RecordId={RecordId}", record.RecordId); throw; }
        catch (Exception ex) {
            Logger.Error(ex, "处理记录原子写入失败，SourceInstanceId={Source}, RecordId={RecordId}", record.SourceInstanceId, record.RecordId);
            return RepositoryResult.Fail<ParcelProcessingWriteResult>("处理记录写入失败。", "ParcelProcessingWriteFailed");
        }
        finally { gate.Release(); }
    }

    /// <summary>查询未关联处理记录，按时间排序并限制单次返回数量。</summary>
    public async Task<IReadOnlyList<ParcelProcessingRecord>> GetUnboundAsync(int limit, CancellationToken cancellationToken) {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        try {
            // 步骤1：每张物理表先过滤并读取有界的窄索引，不合并历史原始报文。
            var latest = new List<UnboundProcessingRecordCandidate>(limit);
            foreach (var suffix in await _partitions.GetReadSuffixesAsync(cancellationToken)) {
                await using var db = await _partitions.CreateContextAsync(suffix, cancellationToken);
                var query = db.Set<ParcelProcessingRecord>().AsNoTracking().Where(row => row.ParcelId == null);
                if (latest.Count == limit) {
                    var oldestIncluded = latest[^1].RecordedAt;
                    query = query.Where(row => row.RecordedAt >= oldestIncluded);
                }
                var candidates = await query.OrderByDescending(row => row.RecordedAt).ThenBy(row => row.Key)
                    .Take(limit).Select(row => new { row.Key, row.RecordedAt })
                    .ToListAsync(cancellationToken);
                latest = latest.Concat(candidates.Select(row => new UnboundProcessingRecordCandidate(row.Key, row.RecordedAt, suffix)))
                    .OrderByDescending(row => row.RecordedAt)
                    .ThenBy(row => row.Key, StringComparer.Ordinal).Take(limit).ToList();
            }
            // 步骤2：仅为最终命中的记录加载完整事实和报文，兼容没有全局凭据的历史记录。
            var records = new Dictionary<(string Suffix, string Key), ParcelProcessingRecord>();
            foreach (var group in latest.GroupBy(row => row.Suffix)) {
                await using var db = await _partitions.CreateContextAsync(group.Key, cancellationToken);
                var keys = group.Select(row => row.Key).ToArray();
                foreach (var record in await db.Set<ParcelProcessingRecord>().AsNoTracking()
                    .Where(row => row.ParcelId == null && keys.Contains(row.Key)).ToListAsync(cancellationToken))
                    records[(group.Key, record.Key)] = record;
            }
            return latest.Where(row => records.ContainsKey((row.Suffix, row.Key)))
                .Select(row => records[(row.Suffix, row.Key)]).ToArray();
        }
        catch (OperationCanceledException exception) {
            Logger.Debug(exception, "未关联处理记录查询已取消。");
            throw;
        }
        catch (Exception exception) {
            Logger.Error(exception, "未关联处理记录查询失败，Limit={Limit}", limit);
            throw;
        }
    }

    /// <summary>相同记录内容重试返回首次结果，相同身份不同内容返回稳定冲突。</summary>
    private static RepositoryResult<ParcelProcessingWriteResult> CheckReceipt(ParcelProcessingReceipt receipt, string hash) => receipt.PayloadHash == hash
        ? RepositoryResult.Success<ParcelProcessingWriteResult>(new() { ParcelId = receipt.ParcelId, PartitionSuffix = receipt.Suffix, IsDuplicate = true })
        : RepositoryResult.Fail<ParcelProcessingWriteResult>("相同RecordId已保存不同内容，禁止覆盖历史记录。", "ParcelProcessingConflict");

    /// <summary>通过不改变值的 EF 更新锁住来源定位行，使跨进程的历史读取和快照刷新顺序提交。</summary>
    private static Task<int> LockParcelLocationAsync(SortingHubDbContext db, string sourceKey, CancellationToken cancellationToken) =>
        db.Set<ParcelLocation>().Where(row => row.SourceKey == sourceKey)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.SourceKey, row => row.SourceKey), cancellationToken);

    /// <summary>只重试提供器明确的唯一键、死锁或串行化冲突；其他异常保留原始失败。</summary>
    private static bool IsRetryableWriteConflict(string? providerName, Exception exception) {
        if (!DatabaseProviderOperations.TryGetProviderErrorNumber(exception, out var number)) return false;
        return providerName switch {
            DbProviderNames.SqlServer => number is 2601 or 2627 or 1205,
            DbProviderNames.Oracle => number is 1 or 60 or 8177,
            DbProviderNames.MySql => number is 1062 or 1205 or 1213,
            _ => false
        };
    }

    /// <summary>使用无歧义的JSON数组计算身份哈希，条码不参与包裹身份。</summary>
    private static string HashIdentity(params string[] parts) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));
}
