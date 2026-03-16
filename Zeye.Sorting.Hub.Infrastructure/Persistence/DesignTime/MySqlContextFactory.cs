using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// 设计时 DbContext 工厂，供 <c>dotnet ef</c> 迁移工具在无宿主进程时构建 <see cref="SortingHubDbContext"/>。
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
    /// 连接字符串优先读取环境变量 <c>MYSQL_CONNECTION_STRING</c>，未设置时使用本地占位值。
    /// 若需执行 <c>database update</c>，请通过 <c>--connection</c> 参数传入真实连接字符串，或设置上述环境变量。
    /// </para>
    /// </remarks>
    internal sealed class MySqlContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {

        /// <summary>
        /// 设计时占位连接字符串，仅在未设置 <c>MYSQL_CONNECTION_STRING</c> 环境变量时使用。
        /// </summary>
        private const string FallbackConnectionString =
            "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid=root;pwd=root;SslMode=None;";

        /// <inheritdoc />
        public SortingHubDbContext CreateDbContext(string[] args) {
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
                ?? FallbackConnectionString;

            var serverVersion = ResolveServerVersion(connectionString);

            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(connectionString, serverVersion)
                .Options;

            return new SortingHubDbContext(options);
        }

        /// <summary>
        /// 按优先级解析 MySQL 服务端版本，不主动锁定版本号：
        /// <list type="number">
        ///   <item><description>
        ///     <c>ServerVersion.AutoDetect</c> — 最优先；数据库可连通时自动探测，无任何版本限制。
        ///   </description></item>
        ///   <item><description>
        ///     环境变量 <c>MYSQL_SERVER_VERSION</c>（格式：<c>8.4.0</c>）— 设计时无数据库时的可配置项，
        ///     适用于 CI/CD 流水线或本地无数据库环境。
        ///   </description></item>
        ///   <item><description>
        ///     兜底 MySQL 8.0 — 仅当前两步均失败时使用。
        ///     此处选用 8.0 是因为它是 Pomelo EF Core MySQL 提供程序当前最主流的 LTS 基准版本；
        ///     该值仅影响 <c>dotnet ef</c> 设计时模型分析，不影响运行时的 <c>ServerVersion.AutoDetect</c> 行为。
        ///   </description></item>
        /// </list>
        /// </summary>
        private static ServerVersion ResolveServerVersion(string connectionString) {
            // 步骤 1：优先 AutoDetect，数据库可连通时最准确且无版本限制
            try {
                return ServerVersion.AutoDetect(connectionString);
            }
            catch {
                // 设计时无数据库连接属正常情况，静默降级到下一步
            }

            // 步骤 2：环境变量覆盖，适用于 CI/CD 或本地无数据库场景
            var envVersion = Environment.GetEnvironmentVariable("MYSQL_SERVER_VERSION");
            if (!string.IsNullOrWhiteSpace(envVersion) &&
                Version.TryParse(envVersion, out var parsedVersion)) {
                return new MySqlServerVersion(parsedVersion);
            }

            // 步骤 3：兜底最小版本，仅用于 dotnet ef 工具链模型分析
            return new MySqlServerVersion(new Version(8, 0, 0));
        }
    }
}

