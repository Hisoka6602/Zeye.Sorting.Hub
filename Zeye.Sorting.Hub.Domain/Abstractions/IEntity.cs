namespace Zeye.Sorting.Hub.Domain.Abstractions {

    public interface IEntity<TPrimaryKey> {
        TPrimaryKey Id { get; set; }
    }
}
