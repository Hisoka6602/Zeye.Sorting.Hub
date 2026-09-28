namespace Zeye.Sorting.Hub.Performance.ParcelAnalyticsBenchmark;

/// <summary>按最近秩定义的P50、P95和P99耗时，单位毫秒。</summary>
internal readonly record struct BenchmarkLatency(decimal P50, decimal P95, decimal P99);
