using Microsoft.EntityFrameworkCore.Metadata;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>基础设施内部的泛型只读映射缓存键，不向业务层暴露 EF 模型。</summary>
internal interface IParcelPartitionReadModelContext {
    /// <summary>来源持久化实体映射。</summary>
    IEntityType SourceEntity { get; }
    /// <summary>窄字段读模型类型。</summary>
    Type ReadModelType { get; }
    /// <summary>物理分表集合的稳定键。</summary>
    string PartitionModelKey { get; }
}
