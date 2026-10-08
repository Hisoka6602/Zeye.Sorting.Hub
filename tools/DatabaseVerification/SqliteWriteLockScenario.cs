using System.Data;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>只在隔离 SQLite 验收库持有写锁，验证超时后发送库重放；不改变业务内容。</summary>
internal static class SqliteWriteLockScenario {
    /// <summary>用 EF 同值更新已有版本行取得写锁，事务结束回滚，持锁范围为 1..60 秒。</summary>
    internal static async Task ExecuteAsync(SortingHubDbContext database, string suffix, string secondsArgument) {
        if (!database.Database.IsSqlite() || !int.TryParse(secondsArgument, out var seconds) || seconds is < 1 or > 60)
            throw new ArgumentException("写锁故障只接受 SQLite 及 1..60 秒持锁时长。");
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var affected = await database.Set<PartitionSchemaVersion>().Where(row => row.Key == "Parcel:" + suffix)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.MigrationId, row => row.MigrationId));
        if (affected != 1) throw new InvalidOperationException("隔离验收库缺少当前分表版本，不能确认写锁已获取。");
        Console.WriteLine("SQLITE_WRITE_LOCK_READY");
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        await transaction.RollbackAsync();
        Console.WriteLine("SQLITE_WRITE_LOCK_RELEASED");
    }
}
