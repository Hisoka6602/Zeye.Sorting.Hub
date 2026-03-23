namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// 配置层数据库提供器标识常量（用于配置值、CLI 参数、ConnectionStrings key）。
    /// </summary>
    internal static class ConfiguredProviderNames {

        /// <summary>
        /// 配置层 MySQL provider key。
        /// </summary>
        internal const string MySql = "MySql";

        /// <summary>
        /// 配置层 SQL Server provider key。
        /// </summary>
        internal const string SqlServer = "SqlServer";
    }
}
