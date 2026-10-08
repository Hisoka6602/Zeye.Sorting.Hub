using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>批量取数内存边界必须覆盖宽行、未知行元数据、溢出及已有显式配置。</summary>
public sealed class OracleQueryFetchInterceptorTests {
    /// <summary>驱动原值不会被缩小，标量查询维持默认缓存，大结果集使用有界批量。</summary>
    [Theory]
    [InlineData(131072, 0, 131072)]
    [InlineData(131072, -1, 131072)]
    [InlineData(131072, 24, 131072)]
    [InlineData(131072, 1024, 1048576)]
    [InlineData(131072, 8192, 2097152)]
    [InlineData(131072, long.MaxValue, 2097152)]
    [InlineData(4194304, 1024, 4194304)]
    public void FetchBufferKeepsExplicitConfigurationAndHasAnOverflowSafeLimit(long current, long rowSize, long expected) =>
        Assert.Equal(expected, OracleQueryFetchInterceptor.FetchBytes(current, rowSize));
}
