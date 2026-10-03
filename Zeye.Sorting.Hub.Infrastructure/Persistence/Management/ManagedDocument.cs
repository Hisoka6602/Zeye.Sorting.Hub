namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
/// <summary>集中保存界面维护数据，版本号用于防止多人覆盖。</summary>
public sealed class ManagedDocument {
    /// <summary>白名单业务文档键。</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>完整业务载荷，敏感文档不直接对外读取。</summary>
    public string Json { get; set; } = string.Empty;
    /// <summary>乐观并发版本。</summary>
    public int Revision { get; set; }
    /// <summary>最后修改的本地时间。</summary>
    public DateTime ModifiedAt { get; set; }
}
