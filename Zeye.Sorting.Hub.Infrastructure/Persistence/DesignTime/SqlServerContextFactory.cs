using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// SQL Server 设计时 DbContext 构建器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 实际的 <c>dotnet ef</c> 入口工厂为 <see cref="MySqlContextFactory"/>（统一入口，按 provider 分发）。
    /// 本类型保留 SQL Server 场景下的配置装配逻辑，便于独立验证与后续扩展。
    /// </para>
    /// <para>SQL Server 路径支持以下场景：</para>
    /// <list type="bullet">
    ///   <item><description><c>dotnet ef migrations add &lt;Name&gt;</c> — 新增迁移</description></item>
    ///   <item><description><c>dotnet ef migrations remove</c> — 回退最新迁移</description></item>
    ///   <item><description><c>dotnet ef database update</c> — 手动推送迁移（通常由 DatabaseInitializerHostedService 在运行时自动完成）</description></item>
    ///   <item><description><c>dotnet ef dbcontext script</c> — 生成 DDL SQL 脚本</description></item>
    /// </list>
    /// <para>
    /// 连接字符串从 <c>appsettings.json</c>（<c>ConnectionStrings:SqlServer</c>）读取。
    /// provider key 与 ConnectionStrings key 语义统一由 <see cref="ConfiguredProviderNames"/> 定义。
    /// 工厂按以下顺序搜索 <c>appsettings.json</c>：
    /// <list type="number">
    ///   <item><description>当前工作目录（适用于从 Host 或解决方案根目录运行 <c>dotnet ef</c>）</description></item>
    ///   <item><description>当前工作目录的相邻 <c>Zeye.Sorting.Hub.Host</c> 子目录（适用于从 Infrastructure 目录运行）</description></item>
    ///   <item><description>向上遍历父目录寻找 <c>Zeye.Sorting.Hub.Host</c> 子目录</description></item>
    ///   <item><description>以上均未找到时使用硬编码占位连接字符串（仅影响设计时工具链，不影响运行时）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class SqlServerContextFactory {

        /// <summary>
        /// 设计时兜底占位连接字符串，仅在无法从 <c>appsettings.json</c> 读取时使用。
        /// 此值仅用于 <c>dotnet ef</c> 工具链的设计时模型分析，不影响运行时连接。
        /// </summary>
        private const string FallbackConnectionString =
            "Server=127.0.0.1,1433;Database=zeye_sorting_hub;User Id=sa;Password=Admin@1234;TrustServerCertificate=True;Encrypt=False;";

        /// <summary>
        /// 为 SQL Server 场景构建设计时 DbContext（供统一设计时工厂内部复用）。
        /// </summary>
        public SortingHubDbContext CreateDbContext(string[] args) {
            var config = DesignTimeConfigurationLocator.LoadConfiguration();
            return CreateDbContext(config);
        }

        /// <summary>
        /// 根据配置创建 SQL Server 设计时 DbContext。
        /// </summary>
        internal SortingHubDbContext CreateDbContext(IConfiguration config) {
            var connectionString = config.GetConnectionString(ConfiguredProviderNames.SqlServer) ?? FallbackConnectionString;

            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new SortingHubDbContext(options);
        }
    }
}
