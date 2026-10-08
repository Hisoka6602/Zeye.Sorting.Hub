using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>耗时窄投影与后台处理事实共用已有保存事务，不额外查询数据库。</summary>
public sealed partial class SortingHubDbContext {
    /// <summary>同步保存也保持原始事实与索引的原子性。</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        IncludeDurationFacts();
        IncludeDwsMeasurements();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>后台异步落库事务同时保存纯内存派生的窄投影。</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
        IncludeDurationFacts();
        IncludeDwsMeasurements();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>仅派生新增的不可变接口事实；失败后再次保存不会重复添加已跟踪投影。</summary>
    private void IncludeDurationFacts() {
        var facts = ChangeTracker.Entries<ParcelProcessingRecord>().Where(entry => entry.State == EntityState.Added
            && ParcelDurationFact.Includes(entry.Entity.Stage)).Select(entry => entry.Entity).ToArray();
        if (facts.Length == 0) return;
        var tracked = ChangeTracker.Entries<ParcelDurationFact>().Select(entry => entry.Entity.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var fact in facts) if (tracked.Add(fact.Key)) Add(ParcelDurationFact.Create(fact));
    }

    /// <summary>新增量测与检测派生窄索引，在已有保存事务内落库，失败重试不重复跟踪同一键。</summary>
    private void IncludeDwsMeasurements() {
        var facts = ChangeTracker.Entries<ParcelProcessingRecord>().Where(entry => entry.State == EntityState.Added
            && ParcelDwsMeasurementSnapshot.Includes(entry.Entity.Stage)).Select(entry => entry.Entity).ToArray();
        if (facts.Length == 0) return;
        var tracked = ChangeTracker.Entries<ParcelDwsMeasurementSnapshot>().Select(entry => entry.Entity.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var fact in facts) if (tracked.Add(fact.Key)) Add(ParcelDwsMeasurementSnapshot.Create(fact));
    }
}
