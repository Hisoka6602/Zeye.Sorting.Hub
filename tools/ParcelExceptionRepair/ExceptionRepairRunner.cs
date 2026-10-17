using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace ParcelExceptionRepair;

/// <summary>有界扫描现存 SQLite 分表，复用生产分类逻辑恢复旧包裹异常；默认不写数据库。</summary>
public sealed class ExceptionRepairRunner(ParcelPartitionStore partitions, string databasePath, IReadOnlyList<ClassificationRule> rules) {
    /// <summary>异常与操作日志，不包含业务原文。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>按首次入库本地日期扫描，每页最多一百票；逐票条件更新支持安全中断及重复执行。</summary>
    public async Task<long> RepairAsync(DateTime from, DateTime until, bool apply, StreamWriter audit, CancellationToken token) {
        if (from.TimeOfDay != TimeSpan.Zero || until.TimeOfDay != TimeSpan.Zero || until <= from || until > from.AddDays(31))
            throw new ArgumentException("修复范围必须为不超过31天的本地半开日期区间。");
        long changed = 0;
        foreach (var suffix in await partitions.GetReadSuffixesAsync(token)) {
            long cursor = 0;
            while (true) {
                await using var db = await partitions.CreateContextAsync(suffix, token);
                var page = await db.Set<Parcel>().AsNoTracking().Where(row => row.Id > cursor
                    && row.CreatedTime >= from && row.CreatedTime < until && row.SourceParcelId != null)
                    .OrderBy(row => row.Id).Take(100).ToArrayAsync(token);
                if (page.Length == 0) break;
                foreach (var parcel in page) {
                    var history = await db.Set<ParcelProcessingRecord>().AsNoTracking().Where(row => row.ParcelId == parcel.Id).ToListAsync(token);
                    if (history.Count == 0) continue;
                    var rebuilt = Parcel.CreateDetected(parcel.Id, history[0], parcel.CreatedTime);
                    rebuilt.ApplyProcessingRecords(history, rules);
                    var before = ExceptionRepairSnapshot.Read(parcel);
                    // 只补回历史质量信息，不清除人工分类，也不改变已完成生命周期。
                    var nextType = before.ExceptionType ?? rebuilt.ExceptionType;
                    var after = before with { ExceptionType = nextType,
                        Status = before.Status == ParcelStatus.Completed ? before.Status : nextType.HasValue ? ParcelStatus.SortingException : before.Status,
                        IsFallbackChuteAssigned = before.IsFallbackChuteAssigned == true || rebuilt.IsFallbackChuteAssigned == true ? true : before.IsFallbackChuteAssigned,
                        IsRoutingBlocked = before.IsRoutingBlocked == true || rebuilt.IsRoutingBlocked == true ? true : before.IsRoutingBlocked };
                    if (before == after) continue;
                    var entry = new ExceptionRepairEntry { DatabasePath = Path.GetFullPath(databasePath), Suffix = suffix,
                        ParcelId = parcel.Id, Before = before, After = after };
                    await audit.WriteLineAsync(JsonSerializer.Serialize(entry).AsMemory(), token);
                    await audit.FlushAsync(token);
                    if (audit.BaseStream is FileStream file) file.Flush(true);
                    if (apply) await UpdateAsync(entry, false, token);
                    changed++;
                    Logger.Info("历史异常修复：Apply={Apply}, Suffix={Suffix}, ParcelId={ParcelId}, Type={Type}", apply, suffix, parcel.Id, nextType);
                }
                cursor = page[^1].Id;
            }
        }
        return changed;
    }

    /// <summary>同一参数化更新语句复核旧值；回滚只恢复本工具涉及字段，不覆盖后续业务变更。</summary>
    public async Task UpdateAsync(ExceptionRepairEntry entry, bool rollback, CancellationToken token) {
        if (!Path.GetFullPath(entry.DatabasePath).Equals(Path.GetFullPath(databasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("审计记录所属数据库与当前数据库不同。");
        ParcelPartitionStore.ValidateSuffix(entry.Suffix);
        var expected = rollback ? entry.After : entry.Before;
        var target = rollback ? entry.Before : entry.After;
        await using var db = await partitions.CreateContextAsync(entry.Suffix, token);
        var current = await db.Set<Parcel>().AsNoTracking().SingleAsync(row => row.Id == entry.ParcelId, token);
        if (ExceptionRepairSnapshot.Read(current) == target) return;
        // SQLite 不支持 EF 为聚合中同表映射生成的 UPDATE FROM 自连接；直接参数化更新这四个快照字段。
        var table = db.Model.FindEntityType(typeof(Parcel))!.GetTableName()!.Replace("\"", "\"\"", StringComparison.Ordinal);
        var sql = $"""
            UPDATE "{table}" SET "Status" = $status, "ExceptionType" = $type,
                "IsFallbackChuteAssigned" = $fallback, "IsRoutingBlocked" = $blocked
            WHERE "Id" = $id AND "Status" = $oldStatus AND "ExceptionType" IS $oldType
                AND "IsFallbackChuteAssigned" IS $oldFallback AND "IsRoutingBlocked" IS $oldBlocked
            """;
        var affected = await db.Database.ExecuteSqlRawAsync(sql, new object[] {
            new SqliteParameter("$id", entry.ParcelId), new SqliteParameter("$status", (int)target.Status),
            new SqliteParameter("$type", (object?)(int?)target.ExceptionType ?? DBNull.Value),
            new SqliteParameter("$fallback", (object?)target.IsFallbackChuteAssigned ?? DBNull.Value),
            new SqliteParameter("$blocked", (object?)target.IsRoutingBlocked ?? DBNull.Value),
            new SqliteParameter("$oldStatus", (int)expected.Status),
            new SqliteParameter("$oldType", (object?)(int?)expected.ExceptionType ?? DBNull.Value),
            new SqliteParameter("$oldFallback", (object?)expected.IsFallbackChuteAssigned ?? DBNull.Value),
            new SqliteParameter("$oldBlocked", (object?)expected.IsRoutingBlocked ?? DBNull.Value)
        }, token);
        if (affected != 1) throw new InvalidOperationException($"包裹 {entry.ParcelId} 的状态已变化，停止修复或回滚。");
        Logger.Info("历史异常条件更新：Rollback={Rollback}, Suffix={Suffix}, ParcelId={ParcelId}", rollback, entry.Suffix, entry.ParcelId);
    }
}
