using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>只读永久清理记录；不提供覆盖、删除或过期回收入口。</summary>
public sealed class ParcelCleanupHistoryService(IDbContextFactory<SortingHubDbContext> factory) {
    /// <summary>与仓储记录保持相同序列化合同。</summary>
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    /// <summary>按数据库分页读取操作历史。</summary>
    public async Task<object> ListAsync(int pageNumber, int pageSize, CancellationToken ct) {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Set<ManagedDocument>().AsNoTracking().Where(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix));
        var totalCount = await query.CountAsync(ct);
        var documents = await query.OrderByDescending(x => x.ModifiedAt).ThenByDescending(x => x.Key).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new { items = documents.Select(x => JsonSerializer.Deserialize<ParcelCleanupAudit>(x.Json, Options)!), totalCount, pageNumber, pageSize };
    }
    /// <summary>只读取固定大小的操作汇总，避免加载历史逐票清单或业务载荷。</summary>
    public async Task<object?> DetailAsync(string id, CancellationToken ct) {
        await using var db = await factory.CreateDbContextAsync(ct);
        var document = await db.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == ParcelCleanupAudit.Prefix + id, ct);
        if (document is null) return null;
        var record = JsonSerializer.Deserialize<ParcelCleanupAudit>(document.Json, Options)!;
        return new { record };
    }
}
