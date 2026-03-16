using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// SQL Server 设计时 DbContext 工厂，供 <c>dotnet ef</c> 迁移工具在无宿主进程时构建 <see cref="SortingHubDbContext"/>。
    /// </summary>
    /// <remarks>
    /// <para>该工厂仅在以下场景被调用：</para>
    /// <list type="bullet">
    ///   <item><description><c>dotnet ef migrations add &lt;Name&gt;</c> — 新增迁移</description></item>
    ///   <item><description><c>dotnet ef migrations remove</c> — 回退最新迁移</description></item>
    ///   <item><description><c>dotnet ef database update</c> — 手动推送迁移（通常由 DatabaseInitializerHostedService 在运行时自动完成）</description></item>
    ///   <item><description><c>dotnet ef dbcontext script</c> — 生成 DDL SQL 脚本</description></item>
    /// </list>
    /// <para>
    /// 连接字符串优先读取环境变量 <c>SQLSERVER_CONNECTION_STRING</c>，未设置时使用本地占位值。
    /// 若需执行 <c>database update</c>，请通过 <c>--connection</c> 参数传入真实连接字符串，或设置上述环境变量。
    /// </para>
    /// </remarks>
    internal sealed class SqlServerContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {

        /// <summary>
        /// 设计时占位连接字符串，仅在未设置 <c>SQLSERVER_CONNECTION_STRING</c> 环境变量时使用。
        /// </summary>
        private const string FallbackConnectionString =
            "Server=127.0.0.1,1433;Database=zeye_sorting_hub;User Id={SQLSERVER_USER};Password={SQLSERVER_PASSWORD};TrustServerCertificate=True;Encrypt=False;";

        /// <inheritdoc />
        public SortingHubDbContext CreateDbContext(string[] args) {
            var connectionString = Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")
                ?? FallbackConnectionString;

            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new SortingHubDbContext(options);
        }
    }
}
