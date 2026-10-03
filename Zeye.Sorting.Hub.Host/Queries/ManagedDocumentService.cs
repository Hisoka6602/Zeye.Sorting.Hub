using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>使用数据库集中存储管理数据，跨平台共享同一持久化语义。</summary>
public sealed class ManagedDocumentService(IDbContextFactory<SortingHubDbContext> factory) {
    /// <summary>按版本原子保存多个文档，个人资料与账号名称不能部分写入。</summary>
    public async Task<bool> WriteBatchAsync((string Key, string Json, int Revision)[] writes, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        foreach (var write in writes) {
            var document = await db.Set<ManagedDocument>().AsTracking().SingleOrDefaultAsync(x => x.Key == write.Key, cancellationToken);
            if ((document?.Revision ?? 0) != write.Revision) return false;
            if (document is null) { document = new ManagedDocument { Key = write.Key }; db.Add(document); }
            document.Json = write.Json; document.Revision = checked(write.Revision + 1); document.ModifiedAt = DateTime.Now;
        }
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException) {
            foreach (var write in writes) {
                if (await ReadAsync(write.Key, cancellationToken) is { } existing && existing.Revision > write.Revision) return false;
            }
            throw;
        }
    }
    /// <summary>读取已保存的管理文档。</summary>
    public async Task<ManagedDocument?> ReadAsync(string key, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
    }
    /// <summary>按客户端读到的版本提交；冲突时拒绝覆盖。</summary>
    public async Task<ManagedDocument?> WriteAsync(string key, string json, int expectedRevision, CancellationToken cancellationToken) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // 生产工厂默认 NoTracking；写入必须显式跟踪，才能真正提交更新并校验旧版本。
        var document = await db.Set<ManagedDocument>().AsTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if ((document?.Revision ?? 0) != expectedRevision) return null;
        if (document is null) { document = new ManagedDocument { Key = key }; db.Add(document); }
        document.Json = json; document.Revision = checked(expectedRevision + 1); document.ModifiedAt = DateTime.Now;
        try { await db.SaveChangesAsync(cancellationToken); return document; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) {
            // 唯一键插入竞争与更新冲突统一返回版本冲突；其他数据库错误继续向上报告。
            if (await ReadAsync(key, cancellationToken) is { } existing && existing.Revision > expectedRevision) return null;
            throw;
        }
    }
}
