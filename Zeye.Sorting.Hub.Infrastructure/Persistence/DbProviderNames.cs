namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// EF Core 数据库提供器名称常量集中定义。
    /// 仓储与迁移均引用此处，避免跨文件重复硬编码，更名/替换时只需修改一处。
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
