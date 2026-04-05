namespace Zeye.Sorting.Hub.Domain.Abstractions {

    /// <summary>领域实体基础接口，定义主键标识约束。</summary>
    /// <typeparam name="TPrimaryKey">主键类型。</typeparam>
    public interface IEntity<TPrimaryKey> {
        /// <summary>实体唯一主键。</summary>
        TPrimaryKey Id { get; set; }
    }
}
