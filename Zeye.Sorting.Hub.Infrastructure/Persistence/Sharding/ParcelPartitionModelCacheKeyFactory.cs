using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>将物理表后缀纳入EF模型缓存，避免跨周期复用错误表名。</summary>
public sealed class ParcelPartitionModelCacheKeyFactory : IModelCacheKeyFactory {
    /// <summary>创建包含分表后缀与设计时标识的模型键。</summary>
    public object Create(DbContext context, bool designTime) => context is SortingHubDbContext sortingContext
        ? (context.GetType(), sortingContext.ParcelPartitionSuffix, sortingContext.AuditPartitionSuffix, designTime)
        : (object)(context.GetType(), designTime);
}
