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
    /// 连接字符串为设计时占位值，<c>dotnet ef</c> 分析模型时通常不需要真实数据库连接；
    /// 但若需要执行 <c>database update</c>，请将连接字符串替换为真实环境的值，或通过
    /// <c>--connection</c> 参数覆盖。
    /// </para>
    /// </remarks>
    internal sealed class MySqlContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {

        /// <summary>
        /// 设计时固定使用 MySQL 8.0，避免 <c>ServerVersion.AutoDetect</c> 在无法连接真实数据库时报错。
        /// </summary>
        private static readonly MySqlServerVersion DesignTimeServerVersion =
            new MySqlServerVersion(new Version(8, 0, 0));

        /// <summary>
        /// 设计时占位连接字符串。
        /// </summary>
        private const string DesignTimeConnectionString =
            "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid=root;pwd=root;SslMode=None;";

        /// <inheritdoc />
        public SortingHubDbContext CreateDbContext(string[] args) {
            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(DesignTimeConnectionString, DesignTimeServerVersion)
                .Options;

            return new SortingHubDbContext(options);
        }
    }
}

