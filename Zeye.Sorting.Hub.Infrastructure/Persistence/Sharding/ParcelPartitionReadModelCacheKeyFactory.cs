using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>按来源 EF 模型、读模型类型和物理分表集合复用只读映射，隔离不同数据库与分表配置。</summary>
internal sealed class ParcelPartitionReadModelCacheKeyFactory : IModelCacheKeyFactory {
    /// <summary>查询日期保持参数化，同一分表集合不会因日期变化重复构建 EF 模型。</summary>
    public object Create(DbContext context, bool designTime) => context is IParcelPartitionReadModelContext read
        ? (context.GetType(), read.SourceEntity, read.ReadModelType, read.PartitionModelKey, designTime)
        : (object)(context.GetType(), designTime);
}
