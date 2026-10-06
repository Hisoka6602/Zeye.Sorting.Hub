using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
namespace Zeye.Sorting.Hub.Host.Queries;
/// <summary>将运维策略保存到共享数据库，并唤醒本实例后台备份轮询。</summary>
public sealed class OperationalPolicyService(IDbContextFactory<SortingHubDbContext> factory, IOptions<BackupOptions> backups, IOptions<ShardingPrebuildOptions> prebuild) {
    /// <summary>记录运维策略通知合并时的异常，不输出策略正文。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>单个等待者使用有界信号，避免重复唤醒堆积。</summary>
    private readonly SemaphoreSlim _changed = new(0, 1);
    /// <summary>策略采用与页面一致的 JSON 合同。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>读取真实已保存策略；首次使用部署设置。</summary>
    public async Task<OperationalPolicy> ReadAsync(CancellationToken ct) {
        var doc = await new ManagedDocumentService(factory).ReadAsync("operations-policy", ct);
        return doc is null ? new() { AutomaticBackups = backups.Value.IsEnabled && !backups.Value.DryRun, BackupIntervalMinutes = Math.Clamp(backups.Value.PollIntervalMinutes, 10, 1440), PrebuildAheadHours = Math.Clamp(prebuild.Value.PrebuildAheadHours, 1, 168) }
            : JsonSerializer.Deserialize<OperationalPolicy>(doc.Json, JsonOptions)! with { Revision = doc.Revision };
    }
    /// <summary>验证范围后按版本提交，冲突时不覆盖其他操作者。</summary>
    public async Task<OperationalPolicy?> WriteAsync(OperationalPolicy policy, CancellationToken ct) {
        if (policy.Revision < 0 || policy.BackupIntervalMinutes is < 10 or > 1440 || policy.PrebuildAheadHours is < 1 or > 168) throw new ArgumentException("备份间隔为 10～1440 分钟，预建窗口为 1～168 小时。");
        var doc = await new ManagedDocumentService(factory).WriteAsync("operations-policy", JsonSerializer.Serialize(policy, JsonOptions), policy.Revision, ct);
        if (doc is null) return null;
        try { _changed.Release(); }
        catch (SemaphoreFullException exception) { Logger.Debug(exception, "运维策略变更通知已合并，最新策略等待后台读取。"); }
        return policy with { Revision = doc.Revision };
    }
    /// <summary>最多一分钟复核共享数据库，使多实例也能及时加载策略变更。</summary>
    public Task<bool> WaitForChangeAsync(CancellationToken ct) => _changed.WaitAsync(TimeSpan.FromMinutes(1), ct);
}
