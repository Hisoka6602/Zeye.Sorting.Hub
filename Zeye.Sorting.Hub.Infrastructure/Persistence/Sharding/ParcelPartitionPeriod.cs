using System.Globalization;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>包裹不可变入库时间对应的物理分表周期。</summary>
public sealed record ParcelPartitionPeriod {
    /// <summary>分表粒度，允许PerDay、PerWeek、PerMonth。</summary>
    public required ParcelTimeShardingGranularity Granularity { get; init; }
    /// <summary>物理表后缀，日为yyyyMMdd、周为yyyyWww、月为yyyyMM。</summary>
    public required string Suffix { get; init; }
    /// <summary>周期开始本地时间，包含边界。</summary>
    public required DateTime Start { get; init; }
    /// <summary>周期结束本地时间，不包含边界。</summary>
    public required DateTime End { get; init; }

    /// <summary>使用ISO周编号和周所属年份解析周期，避免跨年周被拆成两个分表。</summary>
    public static ParcelPartitionPeriod Resolve(DateTime timestamp, ParcelTimeShardingGranularity granularity) {
        if (timestamp == default || timestamp.Kind is not (DateTimeKind.Local or DateTimeKind.Unspecified)) throw new ArgumentException("分表依据必须是有效本地时间。");
        var day = timestamp.Date;
        return granularity switch {
            ParcelTimeShardingGranularity.PerDay => new() { Granularity = granularity, Suffix = day.ToString("yyyyMMdd", CultureInfo.InvariantCulture), Start = day, End = day.AddDays(1) },
            ParcelTimeShardingGranularity.PerWeek => Weekly(day),
            ParcelTimeShardingGranularity.PerMonth => new() { Granularity = granularity, Suffix = day.ToString("yyyyMM", CultureInfo.InvariantCulture), Start = new(day.Year, day.Month, 1), End = new DateTime(day.Year, day.Month, 1).AddMonths(1) },
            _ => throw new ArgumentOutOfRangeException(nameof(granularity), "分表粒度只允许PerDay、PerWeek、PerMonth。")
        };
    }

    /// <summary>计算本地周一边界与ISO周后缀。</summary>
    private static ParcelPartitionPeriod Weekly(DateTime day) {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return new() { Granularity = ParcelTimeShardingGranularity.PerWeek, Suffix = $"{ISOWeek.GetYear(day):D4}W{ISOWeek.GetWeekOfYear(day):D2}", Start = monday, End = monday.AddDays(7) };
    }
}
