using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>使用数据库集中存储管理数据，跨平台共享同一持久化语义。</summary>
public sealed class ManagedDocumentService(IDbContextFactory<SortingHubDbContext> factory, IConfigurationDocumentStore? configurations = null) {
    /// <summary>管理文档版本冲突及写入失败诊断日志，不输出文档内容。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>管理文档低频写入的进程闸门，避免 SQLite 的读写锁升级竞争；数据库版本令牌仍保护跨进程写入。</summary>
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    /// <summary>按版本原子保存多个文档，个人资料与账号名称不能部分写入。</summary>
    public async Task<bool> WriteBatchAsync((string Key, string Json, int Revision)[] writes, CancellationToken cancellationToken) {
        if (configurations is not null && writes.Any(x => IConfigurationDocumentStore.IsConfiguration(x.Key)))
            throw new ArgumentException("配置与业务文档不能混合提交。");
        await WriteGate.WaitAsync(cancellationToken);
        try {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            foreach (var write in writes) {
                var document = await db.Set<ManagedDocument>().AsTracking().SingleOrDefaultAsync(x => x.Key == write.Key, cancellationToken);
                if ((document?.Revision ?? 0) != write.Revision) return false;
                if (document is null) { document = new ManagedDocument { Key = write.Key }; db.Add(document); }
                document.Json = write.Json; document.Revision = checked(write.Revision + 1); document.ModifiedAt = DateTime.Now;
            }
            try {
                await db.SaveChangesAsync(cancellationToken);
                ClassificationRuleSnapshotCache.For(factory).Publish(db.ChangeTracker.Entries<ManagedDocument>().Select(entry => entry.Entity));
                return true;
            }
            catch (DbUpdateConcurrencyException exception) { Logger.Debug(exception, "管理文档批次版本冲突。"); return false; }
            catch (DbUpdateException exception) {
                Logger.Warn(exception, "管理文档批次写入失败，核对唯一键冲突。");
                foreach (var write in writes) {
                    if (await ReadAsync(write.Key, cancellationToken) is { } existing && existing.Revision > write.Revision) return false;
                }
                throw;
            }
        }
        finally { WriteGate.Release(); }
    }
    /// <summary>读取已保存的管理文档。</summary>
    public async Task<ManagedDocument?> ReadAsync(string key, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (configurations is not null && IConfigurationDocumentStore.IsConfiguration(key)) return configurations.Read(key);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
    }
    /// <summary>按客户端读到的版本提交；冲突时拒绝覆盖。</summary>
    public async Task<ManagedDocument?> WriteAsync(string key, string json, int expectedRevision, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (configurations is not null && IConfigurationDocumentStore.IsConfiguration(key)) {
            var saved = configurations.Write(key, json, expectedRevision);
            if (saved is not null) {
                var cache = ClassificationRuleSnapshotCache.For(factory);
                cache.UseConfigurationStore(configurations);
                cache.Publish([saved]);
            }
            return saved;
        }
        await WriteGate.WaitAsync(cancellationToken);
        try {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            // 生产工厂默认 NoTracking；写入必须显式跟踪，才能真正提交更新并校验旧版本。
            var document = await db.Set<ManagedDocument>().AsTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if ((document?.Revision ?? 0) != expectedRevision) return null;
            if (document is null) { document = new ManagedDocument { Key = key }; db.Add(document); }
            document.Json = json; document.Revision = checked(expectedRevision + 1); document.ModifiedAt = DateTime.Now;
            try {
                await db.SaveChangesAsync(cancellationToken);
                ClassificationRuleSnapshotCache.For(factory).Publish([document]);
                return document;
            }
            catch (DbUpdateConcurrencyException exception) { Logger.Debug(exception, "管理文档版本冲突。"); return null; }
            catch (DbUpdateException exception) {
                Logger.Warn(exception, "管理文档写入失败，核对唯一键冲突。");
                // 唯一键插入竞争与更新冲突统一返回版本冲突；其他数据库错误继续向上报告。
                if (await ReadAsync(key, cancellationToken) is { } existing && existing.Revision > expectedRevision) return null;
                throw;
            }
        }
        finally { WriteGate.Release(); }
    }
}
