namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// 慢查询采样记录
    /// </summary>
    /// <param name="CommandText">SQL 文本。</param>
    /// <param name="SqlFingerprint">SQL 指纹。</param>
    /// <param name="ElapsedMilliseconds">执行耗时（毫秒）。</param>
    /// <param name="AffectedRows">影响行数；不可用时为 0。</param>
    /// <param name="IsError">是否异常。</param>
    /// <param name="IsTimeout">是否超时异常。</param>
    /// <param name="IsDeadlock">是否死锁异常。</param>
    /// <param name="OccurredTime">发生时间（本地时间语义）。</param>
    public sealed record SlowQuerySample {
        public string CommandText { get; init; }
        public string SqlFingerprint { get; init; }
        public double ElapsedMilliseconds { get; init; }
        public int AffectedRows { get; init; }
        public bool IsError { get; init; }
        public bool IsTimeout { get; init; }
        public bool IsDeadlock { get; init; }
        public DateTime OccurredTime { get; init; }

        public SlowQuerySample(
            string commandText,
            string sqlFingerprint,
            double elapsedMilliseconds,
            int affectedRows,
            bool isError,
            bool isTimeout,
            bool isDeadlock,
            DateTime occurredTime) {
            CommandText = commandText;
            SqlFingerprint = sqlFingerprint;
            ElapsedMilliseconds = elapsedMilliseconds;
            AffectedRows = affectedRows;
            IsError = isError;
            IsTimeout = isTimeout;
            IsDeadlock = isDeadlock;
            OccurredTime = NormalizeToLocalTime(occurredTime);
        }

        /// <summary>
        /// 将时间值标准化为本地时间语义。
        /// </summary>
        private static DateTime NormalizeToLocalTime(DateTime value) {
            return value.Kind switch {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
                DateTimeKind.Local => value,
                _ => throw new InvalidOperationException("仅支持本地时间语义，请勿传入 UTC 或带 offset 的时间值。")
            };
        }
    }
}
