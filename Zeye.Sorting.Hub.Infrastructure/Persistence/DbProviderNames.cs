namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// EF Core 运行时/迁移 provider name 常量集中定义。
    /// 仅用于 <c>DbContext.Database.ProviderName</c> 或迁移/方言识别，不用于配置值、CLI 参数与连接字符串键名。
    /// 配置层 provider key 请使用 <see cref="ConfiguredProviderNames"/>。
    /// </summary>
    internal static class DbProviderNames {

        /// <summary>
        /// MySQL 提供器名称（Pomelo EF Core MySQL 驱动）。
        /// </summary>
        internal const string MySql = "Pomelo.EntityFrameworkCore.MySql";

        /// <summary>
        /// SQL Server 提供器名称（微软官方 SQL Server EF Core 驱动）。
        /// </summary>
        internal const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
    }
}
