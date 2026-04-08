namespace Zeye.Sorting.Hub.Domain.Options.LogCleanup {

    /// <summary>
    /// 日志清理服务配置项。
    /// <para>所有属性均为 <c>init</c>-only，实例一经构造即不可变，可安全作为并发配置快照存储。</para>
    /// </summary>
    public record class LogCleanupSettings {
        /// <summary>是否启用日志清理。</summary>
        public bool Enabled { get; init; } = true;

        /// <summary>日志文件保留天数（正整数，默认 2 天）。可填写值:大于 0 的整数；若配置 0 或负数，服务将截断至最小值 1 天。</summary>
        public int RetentionDays { get; init; } = 2;

        /// <summary>检查间隔（小时，正整数，默认 1 小时）。可填写值:大于 0 的整数；若配置 0 或负数，服务将截断至最小值 1 小时。</summary>
        public int CheckIntervalHours { get; init; } = 1;

        /// <summary>日志文件所在目录路径（相对于程序工作目录，默认 "logs"）。</summary>
        public string LogDirectory { get; init; } = "logs";
    }
}
