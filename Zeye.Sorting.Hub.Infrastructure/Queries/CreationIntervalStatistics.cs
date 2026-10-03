namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>数据库返回的创建间隔中位数、最小值及间隔总体大小。</summary>
internal sealed record CreationIntervalStatistics {
    /// <summary>创建间隔中位数，毫秒。</summary>
    public decimal? MedianIntervalMilliseconds { get; init; }
    /// <summary>最短的正创建间隔，毫秒。</summary>
    public decimal? MinimumIntervalMilliseconds { get; init; }
    /// <summary>有效间隔数。</summary>
    public long SampleCount { get; init; }
}
