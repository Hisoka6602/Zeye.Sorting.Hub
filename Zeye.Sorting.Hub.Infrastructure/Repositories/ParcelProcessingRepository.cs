using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
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
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>处理记录追加、全局去重与包裹快照的事务仓储。</summary>
public sealed class ParcelProcessingRepository : IParcelProcessingRepository {
    /// <summary>基础表上下文工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>实际分表路由与预建。</summary>
    private readonly ParcelPartitionStore _partitions;
    /// <summary>有界进程内锁，数据库串行化事务提供跨进程保护。</summary>
    private static readonly SemaphoreSlim[] WriteGates = Enumerable.Range(0, 256).Select(static _ => new SemaphoreSlim(1, 1)).ToArray();
    /// <summary>持久化失败与冲突审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>组装处理记录仓储。</summary>
    public ParcelProcessingRepository(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) {
        _factory = factory; _partitions = partitions;
    }

    /// <summary>重试整个事务，原子保存凭据、记录、定位索引与包裹快照。</summary>
    public async Task<RepositoryResult<ParcelProcessingWriteResult>> AppendAsync(ParcelProcessingRecord record, CancellationToken cancellationToken) {
        var sourceKey = HashIdentity(record.SourceInstanceId, record.SourceRunId, record.SourceParcelId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? record.RecordId);
        var recordKey = HashIdentity(record.SourceInstanceId, record.SourceRunId, record.RecordId);
        var gate = WriteGates[Convert.ToByte(sourceKey[..2], 16)];
        await gate.WaitAsync(cancellationToken);
        try {
            record.Validate();
            await using var template = await _factory.CreateDbContextAsync(cancellationToken);
            var strategy = template.Database.CreateExecutionStrategy();
            var isSqlServer = template.Database.IsSqlServer();
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
                await using var db = await _partitions.CreateContextAsync(suffix, cancellationToken);
                // SQL Server 的 SERIALIZABLE 缺失键范围锁会使不同包裹的并发插入互相死锁；
                // 主键与 SourceKey 唯一索引负责跨实例冲突检测，冲突后重试整个事务。
                await using var transaction = await db.Database.BeginTransactionAsync(
                    isSqlServer ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken);
                // 步骤2：事务内复核身份；并发冲突交由整个事务重试，不吞掉已变更的消息。
                existingReceipt = await db.Set<ParcelProcessingReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == recordKey, cancellationToken);
                if (existingReceipt is not null) return CheckReceipt(existingReceipt, record.PayloadHash);
                Parcel? parcel = null;
                if (record.SourceParcelId.HasValue) {
                    location = await db.Set<ParcelLocation>().SingleOrDefaultAsync(x => x.SourceKey == sourceKey, cancellationToken);
                    if (location is null) {
                        var id = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(sourceKey)) & long.MaxValue;
                        if (id == 0) id = 1;
                        // 步骤3：碰撞显式失败，禁止覆盖其他来源或历史基础表的同编号包裹。
                        if (await lookup.Set<Parcel>().AnyAsync(x => x.Id == id, cancellationToken) || await db.Set<ParcelLocation>().AnyAsync(x => x.Id == id, cancellationToken))
                            return RepositoryResult<ParcelProcessingWriteResult>.Fail("包裹来源身份与现有中心编号冲突。", "ParcelSourceConflict");
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
                            return RepositoryResult<ParcelProcessingWriteResult>.Fail(
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
                    var documents = await db.Set<Persistence.Management.ManagedDocument>().AsNoTracking().Where(x => x.Key == "rules-exception" || x.Key == "rules-parcel").ToListAsync(cancellationToken);
                    var ruleJson = documents.SingleOrDefault(x => x.Key == "rules-exception")?.Json;
                    var exceptionRules = ruleJson is null ? ClassificationRuleDefaults.Create() : JsonSerializer.Deserialize<ClassificationRule[]>(ruleJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    var parcelJson = documents.SingleOrDefault(x => x.Key == "rules-parcel")?.Json;
                    var rules = parcelJson is null ? exceptionRules : exceptionRules.Concat(JsonSerializer.Deserialize<ClassificationRule[]>(parcelJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToArray();
                    parcel.ApplyProcessingRecords(history, rules);
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return RepositoryResult<ParcelProcessingWriteResult>.Success(new() { ParcelId = parcel?.Id, PartitionSuffix = suffix });
            }
            for (var conflictAttempt = 0; ; conflictAttempt++) {
                try { return await strategy.ExecuteAsync(AppendOnceAsync); }
                catch (DbUpdateException ex) when (isSqlServer && IsSqlServerUniqueConflict(ex) && conflictAttempt < 4) {
                    Logger.Warn(ex, "处理记录唯一键竞争，重试完整事务，RecordId={RecordId}, Attempt={Attempt}", record.RecordId, conflictAttempt + 1);
                    await Task.Delay(5 * (conflictAttempt + 1), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException ex) { Logger.Warn(ex, "处理记录写入已取消，RecordId={RecordId}", record.RecordId); throw; }
        catch (Exception ex) {
            Logger.Error(ex, "处理记录原子写入失败，SourceInstanceId={Source}, RecordId={RecordId}", record.SourceInstanceId, record.RecordId);
            return RepositoryResult<ParcelProcessingWriteResult>.Fail("处理记录写入失败。", "ParcelProcessingWriteFailed");
        }
        finally { gate.Release(); }
    }

    /// <summary>查询未关联处理记录，按时间排序并限制单次返回数量。</summary>
    public async Task<IReadOnlyList<ParcelProcessingRecord>> GetUnboundAsync(int limit, CancellationToken cancellationToken) {
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await (await ParcelPartitionQueryBuilder.BuildAsync<ParcelProcessingRecord>(db, _partitions, cancellationToken))
            .Where(x => x.ParcelId == null).OrderByDescending(x => x.RecordedAt).ThenBy(x => x.Key).Take(limit).ToListAsync(cancellationToken);
    }

    /// <summary>相同记录内容重试返回首次结果，相同身份不同内容返回稳定冲突。</summary>
    private static RepositoryResult<ParcelProcessingWriteResult> CheckReceipt(ParcelProcessingReceipt receipt, string hash) => receipt.PayloadHash == hash
        ? RepositoryResult<ParcelProcessingWriteResult>.Success(new() { ParcelId = receipt.ParcelId, PartitionSuffix = receipt.Suffix, IsDuplicate = true })
        : RepositoryResult<ParcelProcessingWriteResult>.Fail("相同RecordId已保存不同内容，禁止覆盖历史记录。", "ParcelProcessingConflict");

    /// <summary>SQL Server 在已提交的并发事务插入相同凭据或来源键时返回唯一约束冲突。</summary>
    private static bool IsSqlServerUniqueConflict(Exception exception) => exception switch {
        SqlException sql => sql.Number is 2601 or 2627,
        { InnerException: { } inner } => IsSqlServerUniqueConflict(inner),
        _ => false
    };

    /// <summary>使用无歧义的JSON数组计算身份哈希，条码不参与包裹身份。</summary>
    private static string HashIdentity(params string[] parts) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));
}
