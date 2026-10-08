namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// 配置层数据库提供器标识常量（用于配置值、CLI 参数、ConnectionStrings key）。
    /// </summary>
    public static class ConfiguredProviderNames {

        /// <summary>
        /// 配置层 MySQL provider key。
        /// </summary>
        public const string MySql = "MySql";

        /// <summary>
        /// 配置层 SQL Server provider key。
        /// </summary>
        public const string SqlServer = "SqlServer";

        /// <summary>Oracle 配置键；业务库对应目标 PDB 中的用户 schema。</summary>
        public const string Oracle = "Oracle";

        /// <summary>SQLite 配置键；业务库对应持久化数据库文件。</summary>
        public const string SQLite = "SQLite";

        /// <summary>统一验证提供器名称，并兼容 MSSQL、Sqlite 的既有命名。</summary>
        public static string Normalize(string? provider) => provider?.Trim().ToUpperInvariant() switch {
            "MYSQL" => MySql,
            "SQLSERVER" or "MSSQL" => SqlServer,
            "ORACLE" => Oracle,
            "SQLITE" => SQLite,
            _ => throw new InvalidOperationException($"不支持的 Persistence:Provider={provider}，可选值：MySql / SqlServer / Oracle / SQLite。")
        };
    }
}
