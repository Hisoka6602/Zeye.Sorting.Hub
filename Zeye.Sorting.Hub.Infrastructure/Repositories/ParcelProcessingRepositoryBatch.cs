using System.Buffers.Binary;
using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories;

/// <summary>同一包裹的有界事实批次共用一次事务、历史读取和快照刷新。</summary>
public sealed partial class ParcelProcessingRepository {
    /// <summary>逐条保留不可变凭据和业务冲突，所有新事实与最终快照原子提交。</summary>
    public async Task<IReadOnlyList<RepositoryResult<ParcelProcessingWriteResult>>> AppendBatchAsync(
        IReadOnlyList<ParcelProcessingRecord> records, CancellationToken cancellationToken) {
        if (records.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(records));
        if (records.Count == 1) return [await AppendAsync(records[0], cancellationToken)];
        var first = records[0];
        if (first.SourceParcelId is null || records.Any(record => record.SourceInstanceId != first.SourceInstanceId
            || record.SourceRunId != first.SourceRunId || record.SourceParcelId != first.SourceParcelId))
            throw new ArgumentException("一个批次必须属于同一来源、会话和包裹。", nameof(records));
        foreach (var record in records) record.Validate();
        var sourceKey = HashIdentity(first.SourceInstanceId, first.SourceRunId, first.SourceParcelId.Value.ToString(CultureInfo.InvariantCulture));
        var keys = records.Select(record => HashIdentity(record.SourceInstanceId, record.SourceRunId, record.RecordId)).ToArray();
        var distinctKeys = keys.Distinct(StringComparer.Ordinal).ToArray();
        var gate = WriteGates[Convert.ToByte(sourceKey[..2], 16)];
        await gate.WaitAsync(cancellationToken);
        try {
            var rules = await _rules.GetAsync(cancellationToken);
            await using var template = await _factory.CreateDbContextAsync(cancellationToken);
            var strategy = template.Database.CreateExecutionStrategy();
            var isSqlServer = template.Database.IsSqlServer();
            /// <summary>每次重试创建新上下文，禁止把失败事务的跟踪状态带到下一次。</summary>
            async Task<IReadOnlyList<RepositoryResult<ParcelProcessingWriteResult>>> AppendOnceAsync() {
                await using var lookup = await _factory.CreateDbContextAsync(cancellationToken);
                var known = await lookup.Set<ParcelProcessingReceipt>().AsNoTracking()
                    .Where(row => distinctKeys.Contains(row.Key)).ToDictionaryAsync(row => row.Key, cancellationToken);
                if (distinctKeys.All(known.ContainsKey))
                    return records.Select((record, index) => CheckReceipt(known[keys[index]], record.PayloadHash)).ToArray();
                var location = await lookup.Set<ParcelLocation>().AsNoTracking()
                    .SingleOrDefaultAsync(row => row.SourceKey == sourceKey, cancellationToken);
                var firstNew = records.First((record) => !known.ContainsKey(HashIdentity(record.SourceInstanceId, record.SourceRunId, record.RecordId)));
                var period = _partitions.Resolve(firstNew.RecordedAt);
                var suffix = location?.Suffix ?? period.Suffix;
                if (location is null) await _partitions.EnsureCreatedAsync(period, cancellationToken);
                var newParcelId = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(sourceKey)) & long.MaxValue;
                if (newParcelId == 0) newParcelId = 1;
                // 事务占用连接前完成遗留基础表检查，不在满池时等待第二条连接。
                var baseIdCollision = location is null && await lookup.Set<Parcel>().AnyAsync(row => row.Id == newParcelId, cancellationToken);
                await using var db = await _partitions.CreateContextAsync(suffix, cancellationToken);
                await using var transaction = await db.Database.BeginTransactionAsync(
                    isSqlServer ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, cancellationToken);
                known = await db.Set<ParcelProcessingReceipt>().AsNoTracking()
                    .Where(row => distinctKeys.Contains(row.Key)).ToDictionaryAsync(row => row.Key, cancellationToken);
                var outcomes = new RepositoryResult<ParcelProcessingWriteResult>[records.Count];
                if (distinctKeys.All(known.ContainsKey))
                    return records.Select((record, index) => CheckReceipt(known[keys[index]], record.PayloadHash)).ToArray();
                location = await db.Set<ParcelLocation>().SingleOrDefaultAsync(row => row.SourceKey == sourceKey, cancellationToken);
                Parcel parcel;
                List<ParcelProcessingRecord> history;
                if (location is null) {
                    if (baseIdCollision || await db.Set<ParcelLocation>().AnyAsync(row => row.Id == newParcelId, cancellationToken))
                        return records.Select(_ => RepositoryResult<ParcelProcessingWriteResult>.Fail("包裹来源身份与现有中心编号冲突。", "ParcelSourceConflict")).ToArray();
                    firstNew = records.First(record => !known.ContainsKey(HashIdentity(record.SourceInstanceId, record.SourceRunId, record.RecordId)));
                    parcel = Parcel.CreateDetected(newParcelId, firstNew, firstNew.RecordedAt);
                    location = new ParcelLocation { Id = newParcelId, SourceKey = sourceKey, Suffix = suffix, CreatedTime = firstNew.RecordedAt };
                    db.Add(location); db.Add(parcel);
                    history = [];
                }
                else {
                    if (location.Suffix != suffix) throw new InvalidOperationException("并发写入已固定其他分表，请使用相同消息重试。");
                    parcel = await db.Set<Parcel>().AsTracking().SingleAsync(row => row.Id == location.Id, cancellationToken);
                    history = await db.Set<ParcelProcessingRecord>().AsNoTracking().Where(row => row.ParcelId == parcel.Id).ToListAsync(cancellationToken);
                }
                var detected = history.Any(record => record.Stage == ParcelProcessingStage.Detected);
                var appended = 0;
                for (var index = 0; index < records.Count; index++) {
                    var record = records[index];
                    if (known.TryGetValue(keys[index], out var previous)) {
                        outcomes[index] = CheckReceipt(previous, record.PayloadHash);
                        continue;
                    }
                    if (record.Stage == ParcelProcessingStage.Detected && detected) {
                        outcomes[index] = RepositoryResult<ParcelProcessingWriteResult>.Fail(
                            "同一来源会话和包裹编号已存在检测记录；重试必须复用RecordId，设备计数重置必须更换SourceRunId。", "ParcelSourceConflict");
                        continue;
                    }
                    var stored = record with { Key = keys[index], ParcelId = parcel.Id, PartitionTime = location.CreatedTime };
                    var receipt = new ParcelProcessingReceipt { Key = keys[index], PayloadHash = record.PayloadHash,
                        ParcelId = parcel.Id, Suffix = suffix, RecordedAt = record.RecordedAt };
                    db.Add(stored); db.Add(receipt); known.Add(keys[index], receipt); history.Add(stored);
                    if (record.Stage == ParcelProcessingStage.Detected) detected = true;
                    appended++;
                    outcomes[index] = RepositoryResult<ParcelProcessingWriteResult>.Success(new() { ParcelId = parcel.Id, PartitionSuffix = suffix });
                }
                if (appended > 0) {
                    parcel.ApplyProcessingRecords(history, rules);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                return outcomes;
            }
            for (var attempt = 0; ; attempt++) {
                try { return await strategy.ExecuteAsync(AppendOnceAsync); }
                catch (DbUpdateException exception) when (isSqlServer && IsSqlServerUniqueConflict(exception) && attempt < 4) {
                    Logger.Warn(exception, "包裹批次唯一键竞争，重试完整事务，Source={Source}, Parcel={Parcel}", first.SourceInstanceId, first.SourceParcelId);
                    await Task.Delay(5 * (attempt + 1), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) {
            Logger.Error(exception, "包裹批次原子写入失败，Source={Source}, Parcel={Parcel}", first.SourceInstanceId, first.SourceParcelId);
            return records.Select(_ => RepositoryResult<ParcelProcessingWriteResult>.Fail("处理记录批次写入失败。", "ParcelProcessingWriteFailed")).ToArray();
        }
        finally { gate.Release(); }
    }
}
