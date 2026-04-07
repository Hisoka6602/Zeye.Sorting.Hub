namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// 慢查询采样记录，保存单条 SQL 命令的执行耗时、影响行数及异常状态快照。
    /// </summary>
    public sealed record SlowQuerySample {
        /// <summary>原始 SQL 命令文本。</summary>
        public string CommandText { get; init; }

        /// <summary>SQL 指纹（标准化后的哈希标识，用于聚合统计）。</summary>
        public string SqlFingerprint { get; init; }

        /// <summary>执行耗时（毫秒）。</summary>
        public double ElapsedMilliseconds { get; init; }

        /// <summary>影响行数；不可用时为 0。</summary>
        public int AffectedRows { get; init; }

        /// <summary>是否发生异常。</summary>
        public bool IsError { get; init; }

        /// <summary>是否超时异常。</summary>
        public bool IsTimeout { get; init; }

        /// <summary>是否死锁异常。</summary>
        public bool IsDeadlock { get; init; }

        /// <summary>发生时间（本地时间语义）。</summary>
        public DateTime OccurredTime { get; init; }

        /// <summary>
        /// 初始化慢查询采样记录。
        /// </summary>
        /// <param name="commandText">原始 SQL 命令文本。</param>
        /// <param name="sqlFingerprint">SQL 指纹（标准化后的哈希标识，用于聚合统计）。</param>
        /// <param name="elapsedMilliseconds">执行耗时，单位为毫秒。</param>
        /// <param name="affectedRows">影响行数；不可用时为 0。</param>
        /// <param name="isError">指示本次执行是否发生异常。</param>
        /// <param name="isTimeout">指示本次异常是否为超时异常。</param>
        /// <param name="isDeadlock">指示本次异常是否为死锁异常。</param>
        /// <param name="occurredTime">采样发生时间，保持本地时间语义。</param>
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
            OccurredTime = AutoTuningConfigurationReader.NormalizeToLocalTime(occurredTime);
        }
    }
}
