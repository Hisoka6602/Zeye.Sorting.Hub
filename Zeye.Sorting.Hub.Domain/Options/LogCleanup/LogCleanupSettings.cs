namespace Zeye.Sorting.Hub.Domain.Options.LogCleanup {

    /// <summary>日志清理服务配置项。</summary>
    public record class LogCleanupSettings {
        /// <summary>是否启用日志清理。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>日志文件保留天数（正整数，默认 2 天）。可填写值:大于 0 的整数；若配置 0 或负数，服务将回退为默认值 2。</summary>
        public int RetentionDays { get; set; } = 2;

        /// <summary>检查间隔（小时，正整数，默认 1 小时）。可填写值:大于 0 的整数；若配置 0 或负数，服务将回退为默认值 1。</summary>
        public int CheckIntervalHours { get; set; } = 1;

        /// <summary>日志文件所在目录路径（相对于程序工作目录，默认 "logs"）。</summary>
        public string LogDirectory { get; set; } = "logs";
    }
}
