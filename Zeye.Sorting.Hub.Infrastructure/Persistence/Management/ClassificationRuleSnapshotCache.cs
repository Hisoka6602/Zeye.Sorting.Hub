using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>分类配置低频加载及提交后发布，高频事实处理只复用不可变规则快照。</summary>
public sealed class ClassificationRuleSnapshotCache {
    /// <summary>按上下文工厂隔离缓存，避免不同数据库或测试环境共享规则。</summary>
    private static readonly ConditionalWeakTable<IDbContextFactory<SortingHubDbContext>, ClassificationRuleSnapshotCache> Caches = new();
    /// <summary>与管理端一致的规则序列化设置。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>不包含规则正文及凭据的异常日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>统一 EF Core 工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>后台刷新单飞闸门，冷启动也复用同一加载。</summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    /// <summary>原子更新两类规则和发布版本的内存锁。</summary>
    private readonly object _publishGate = new();
    /// <summary>已提交的异常分类规则，初值为系统内置规则。</summary>
    private IReadOnlyList<ClassificationRule> _exceptionRules = ClassificationRuleDefaults.Create().ToImmutableArray();
    /// <summary>已提交的包裹分类规则。</summary>
    private IReadOnlyList<ClassificationRule> _parcelRules = ImmutableArray<ClassificationRule>.Empty;
    /// <summary>异常规则的耐久版本，较旧的后台快照不得覆盖新提交。</summary>
    private int _exceptionRevision;
    /// <summary>包裹规则的耐久版本。</summary>
    private int _parcelRevision;
    /// <summary>已确认初始化，空配置同样属于有效快照。</summary>
    private bool _initialized;
    /// <summary>对处理链路一次发布的合并规则。</summary>
    private IReadOnlyList<ClassificationRule>? _snapshot;

    /// <summary>工厂拥有唯一的规则缓存，不引入新的数据库连接配置。</summary>
    private ClassificationRuleSnapshotCache(IDbContextFactory<SortingHubDbContext> factory) { _factory = factory; }

    /// <summary>同一数据库工厂的后台加载、管理保存和事实处理共用一个快照。</summary>
    public static ClassificationRuleSnapshotCache For(IDbContextFactory<SortingHubDbContext> factory) =>
        Caches.GetValue(factory, static source => new(source));

    /// <summary>启动预热后只读内存，独立测试或工具首次调用时仍能正确加载已持久化规则。</summary>
    public async Task<IReadOnlyList<ClassificationRule>> GetAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Volatile.Read(ref _initialized)) await RefreshAsync(cancellationToken, onlyIfMissing: true);
        return Volatile.Read(ref _snapshot)!;
    }

    /// <summary>由启动和后台任务刷新其他实例发布的规则，成功之前保持上一份已提交快照。</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken, bool onlyIfMissing = false) {
        await _refreshGate.WaitAsync(cancellationToken);
        try {
            if (onlyIfMissing && Volatile.Read(ref _initialized)) return;
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var documents = await db.Set<ManagedDocument>().AsNoTracking()
                .Where(document => document.Key == "rules-exception" || document.Key == "rules-parcel").ToArrayAsync(cancellationToken);
            Publish(documents);
            lock (_publishGate) {
                if (_snapshot is null) Volatile.Write(ref _snapshot, _exceptionRules.Concat(_parcelRules).ToImmutableArray());
                Volatile.Write(ref _initialized, true);
            }
        }
        catch (Exception exception) {
            Logger.Error(exception, "分类规则快照刷新失败，保留已发布规则。");
            throw;
        }
        finally { _refreshGate.Release(); }
    }

    /// <summary>管理写入耐久提交后立即发布；失败、回滚和旧版本不能改变执行规则。</summary>
    public void Publish(IEnumerable<ManagedDocument> documents) {
        var updates = documents.Where(document => document.Key is "rules-exception" or "rules-parcel").Select(document =>
            (document.Key, document.Revision, Rules: (IReadOnlyList<ClassificationRule>)(
                JsonSerializer.Deserialize<ClassificationRule[]>(document.Json, JsonOptions)
                ?? throw new JsonException("分类规则必须为有效数组。")).ToImmutableArray())).ToArray();
        if (updates.Length == 0) return;
        lock (_publishGate) {
            foreach (var update in updates) {
                if (update.Key == "rules-exception" && update.Revision > _exceptionRevision) {
                    _exceptionRules = update.Rules; _exceptionRevision = update.Revision;
                }
                else if (update.Key == "rules-parcel" && update.Revision > _parcelRevision) {
                    _parcelRules = update.Rules; _parcelRevision = update.Revision;
                }
            }
            Volatile.Write(ref _snapshot, _exceptionRules.Concat(_parcelRules).ToImmutableArray());
        }
    }
}
