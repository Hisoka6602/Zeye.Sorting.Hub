using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>纯十进制统计，零基准不制造百分比，阈值同时比较绝对与相对差。</summary>
internal static class ParcelDwsConsistencyMath {
    /// <summary>空总体保持未知，偶数中位数保留十进制精度。</summary>
    internal static decimal? Median(IEnumerable<decimal> values) => OrderedMedian(values.Order().ToArray());
    /// <summary>已排序指标复用数组，避免重量、体积及扫码耗时再次排序。</summary>
    private static decimal? OrderedMedian(decimal[] ordered) =>
        ordered.Length == 0 ? null : ordered[(ordered.Length - 1) / 2] / 2m + ordered[ordered.Length / 2] / 2m;
    /// <summary>量测差除以正基准，真实零测量仍保留但百分比不可计算。</summary>
    internal static decimal? Percent(decimal? difference, decimal? baseline) => difference.HasValue && baseline > 0 ? difference / baseline * 100m : null;
    /// <summary>有效值独立统计，至少两次才计算极差，参考值不替代观测值。</summary>
    internal static DwsMeasurementMetric Metric(IEnumerable<decimal?> values, decimal? reference) {
        var ordered = values.Where(value => value is >= 0).Select(value => value!.Value).Order().ToArray();
        var median = OrderedMedian(ordered);
        var spread = ordered.Length >= 2 ? ordered[^1] - ordered[0] : (decimal?)null;
        var deviation = reference.HasValue && ordered.Length > 0 ? Math.Max(Math.Abs(ordered[0] - reference.Value), Math.Abs(ordered[^1] - reference.Value)) : (decimal?)null;
        return new() { Count = ordered.Length, Minimum = ordered.Length > 0 ? ordered[0] : null, Maximum = ordered.Length > 0 ? ordered[^1] : null,
            Median = median, Average = ordered.Length > 0 ? ordered.Average() : null,
            P95 = ordered.Length > 0 ? ordered[(int)decimal.Ceiling(ordered.Length * .95m) - 1] : null,
            Spread = spread, SpreadPercent = Percent(spread, median), Reference = reference,
            MaximumReferenceDeviation = deviation, MaximumReferenceDeviationPercent = Percent(deviation, reference) };
    }
    /// <summary>常见的单次量测直接计算，避免创建指标数组和排序迭代器；缺失值及负值仍保持未知。</summary>
    internal static DwsMeasurementMetric SingleMetric(decimal? value, decimal? reference) {
        if (value is not >= 0) return new() { Reference = reference };
        var deviation = reference.HasValue ? Math.Abs(value.Value - reference.Value) : (decimal?)null;
        return new() { Count = 1, Minimum = value, Maximum = value, Median = value, Average = value, P95 = value,
            Reference = reference, MaximumReferenceDeviation = deviation,
            MaximumReferenceDeviationPercent = Percent(deviation, reference) };
    }
    /// <summary>相对基准未知时不贸然判断异常。</summary>
    internal static bool Exceeds(decimal? difference, decimal? percent, decimal absoluteTolerance, decimal relativeTolerance) =>
        difference > absoluteTolerance && percent > relativeTolerance;
    /// <summary>仅使用物理体积或完整正尺寸，排除体积重量，溢出保持未知。</summary>
    internal static decimal? Volume(ParcelDwsMeasurementSnapshot record) {
        if (record.VolumeMm3 is > 0) return record.VolumeMm3 / 1000m;
        if (record.LengthMm is not > 0 || record.WidthMm is not > 0 || record.HeightMm is not > 0) return null;
        try { return checked(record.LengthMm * record.WidthMm * record.HeightMm) / 1000m; }
        catch (OverflowException) { return null; }
    }
}
