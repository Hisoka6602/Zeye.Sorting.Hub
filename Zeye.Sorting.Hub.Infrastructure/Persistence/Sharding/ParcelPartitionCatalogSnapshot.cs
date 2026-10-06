using System.Collections.Immutable;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>一次发布的不可变分表目录，热查询无需再次读取数据库元数据。</summary>
public sealed class ParcelPartitionCatalogSnapshot {
    /// <summary>完整历史周期，按开始时间倒序排列。</summary>
    public IReadOnlyList<ParcelPartitionPeriod> Periods { get; }
    /// <summary>全部物理分表及历史基础表的后缀。</summary>
    public IReadOnlyList<string> Suffixes { get; }
    /// <summary>构造查询共享的只读目录集合。</summary>
    internal ParcelPartitionCatalogSnapshot(IEnumerable<ParcelPartitionPeriod> periods) {
        var ordered = periods.OrderByDescending(period => period.Start).ThenBy(period => period.Suffix, StringComparer.Ordinal).ToImmutableArray();
        Periods = ordered;
        Suffixes = ordered.Select(period => period.Suffix).Append(string.Empty).ToImmutableArray();
    }
}
