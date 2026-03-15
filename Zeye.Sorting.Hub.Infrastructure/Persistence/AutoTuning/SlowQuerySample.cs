namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// 慢查询采样记录
    /// </summary>
    /// <param name="CommandText">SQL 文本。</param>
    /// <param name="ElapsedMilliseconds">执行耗时（毫秒）。</param>
    /// <param name="OccurredTime">发生时间（本地时间语义）。</param>
    public sealed record SlowQuerySample {
        public string CommandText { get; init; }
        public double ElapsedMilliseconds { get; init; }
        public DateTime OccurredTime { get; init; }

        public SlowQuerySample(string commandText, double elapsedMilliseconds, DateTime occurredTime) {
            CommandText = commandText;
            ElapsedMilliseconds = elapsedMilliseconds;
            OccurredTime = NormalizeToLocalTime(occurredTime);
        }

        private static DateTime NormalizeToLocalTime(DateTime value) {
            return value.Kind switch {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
                DateTimeKind.Local => value,
                _ => throw new InvalidOperationException("仅支持本地时间语义，请勿传入 UTC 或带 offset 的时间值。")
            };
        }
    }
}
