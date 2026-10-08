using System.Globalization;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>单次量测优化必须保持缺失、真实零值、十进制指标与参考偏差的既有业务含义。</summary>
public sealed class ParcelDwsSingleMeasurementTests {
    /// <summary>与独立的通用多样本路径逐字段对照，单样本不制造可比较极差。</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("-1", "100")]
    [InlineData("0", null)]
    [InlineData("0", "0")]
    [InlineData("12.345", "10")]
    [InlineData("12.345", "0")]
    [InlineData("1234.567", null)]
    public void SingleMeasurementPreservesGeneralStatistics(string? valueText, string? referenceText) {
        var value = valueText is null ? (decimal?)null : decimal.Parse(valueText, CultureInfo.InvariantCulture);
        var reference = referenceText is null ? (decimal?)null : decimal.Parse(referenceText, CultureInfo.InvariantCulture);
        var result = ParcelDwsConsistencyMath.SingleMetric(value, reference);
        Assert.Equal(ParcelDwsConsistencyMath.Metric([value], reference), result);
        Assert.Equal(value is >= 0 ? 1 : 0, result.Count);
        Assert.Null(result.Spread);
        Assert.Null(result.SpreadPercent);
        if (reference is null or <= 0) Assert.Null(result.MaximumReferenceDeviationPercent);
    }
}
