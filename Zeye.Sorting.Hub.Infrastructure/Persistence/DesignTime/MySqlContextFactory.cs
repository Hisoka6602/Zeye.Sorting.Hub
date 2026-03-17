using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// 设计时 DbContext 工厂（统一入口），供 <c>dotnet ef</c> 迁移工具在无宿主进程时构建 <see cref="SortingHubDbContext"/>。
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
    /// 默认按 <c>Persistence:Provider</c> 解析数据库提供器（支持 <c>MySql</c> / <c>SqlServer</c>），也支持通过
    /// <c>dotnet ef ... -- --provider SqlServer</c> 显式覆盖提供器。
    /// </para>
    /// <para>
    /// 连接字符串从 <c>appsettings.json</c>（<c>ConnectionStrings:MySql</c> / <c>ConnectionStrings:SqlServer</c>）读取。
    /// 工厂按以下顺序搜索 <c>appsettings.json</c>：
    /// <list type="number">
    ///   <item><description>当前工作目录（适用于从 Host 或解决方案根目录运行 <c>dotnet ef</c>）</description></item>
    ///   <item><description>当前工作目录的相邻 <c>Zeye.Sorting.Hub.Host</c> 子目录（适用于从 Infrastructure 目录运行）</description></item>
    ///   <item><description>向上遍历父目录寻找 <c>Zeye.Sorting.Hub.Host</c> 子目录</description></item>
    ///   <item><description>以上均未找到时使用硬编码占位连接字符串（仅影响设计时工具链，不影响运行时）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class MySqlContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {
        private const string MySqlProviderName = "MySql";
        private const string SqlServerProviderName = "SqlServer";
        private const string ProviderArgumentName = "--provider";

        /// <summary>
        /// 设计时兜底占位连接字符串，仅在无法从 <c>appsettings.json</c> 读取时使用。
        /// 此值仅用于 <c>dotnet ef</c> 工具链的设计时模型分析，不影响运行时连接。
        /// </summary>
        private const string FallbackConnectionString =
            "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid=root;pwd=Admin@1234;SslMode=None;";

        /// <summary>
        /// 向上遍历父目录时的最大层级数，防止无限递归到文件系统根目录。
        /// </summary>
        private const int MaxParentDirectorySearchDepth = 6;

        /// <inheritdoc />
        /// <summary>
        /// 方法：CreateDbContext。
        /// </summary>
        public SortingHubDbContext CreateDbContext(string[] args) {
            var config = LoadConfiguration();
            var provider = ResolveProvider(args, config);

            if (string.Equals(provider, SqlServerProviderName, StringComparison.OrdinalIgnoreCase)) {
                var factory = new SqlServerContextFactory();
                return factory.CreateDbContext(config);
            }

            var connectionString = config.GetConnectionString(MySqlProviderName) ?? FallbackConnectionString;
            var serverVersion = ResolveServerVersion(connectionString);
            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(connectionString, serverVersion)
                .Options;
            return new SortingHubDbContext(options);
        }

        /// <summary>
        /// 方法：ResolveProvider。
        /// </summary>
        private static string ResolveProvider(string[] args, IConfiguration config) {
            for (var i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (string.Equals(arg, ProviderArgumentName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) {
                    return NormalizeProvider(args[i + 1]);
                }

                if (arg.StartsWith($"{ProviderArgumentName}=", StringComparison.OrdinalIgnoreCase)) {
                    if (arg.Length == ProviderArgumentName.Length + 1) {
                        throw new InvalidOperationException("参数 '--provider=' 未提供值。可选值：MySql / SqlServer。");
                    }

                    var provided = arg[(ProviderArgumentName.Length + 1)..];
                    return NormalizeProvider(provided);
                }
            }

            return NormalizeProvider(config["Persistence:Provider"] ?? MySqlProviderName);
        }

        /// <summary>
        /// 方法：NormalizeProvider。
        /// </summary>
        private static string NormalizeProvider(string? provider) {
            if (string.IsNullOrWhiteSpace(provider)) {
                throw new InvalidOperationException("数据库提供器不能为空。可选值：MySql / SqlServer。");
            }

            if (string.Equals(provider, SqlServerProviderName, StringComparison.OrdinalIgnoreCase)) {
                return SqlServerProviderName;
            }

            if (string.Equals(provider, MySqlProviderName, StringComparison.OrdinalIgnoreCase)) {
                return MySqlProviderName;
            }

            throw new InvalidOperationException($"不支持的数据库提供器：{provider}。可选值：MySql / SqlServer。");
        }

        /// <summary>
        /// 从 <c>appsettings.json</c> 加载配置。
        /// 按优先级搜索以下路径：当前工作目录 → 相邻 Host 目录 → 向上遍历父目录中的 Host 子目录。
        /// </summary>
        /// <summary>
        /// 方法：LoadConfiguration。
        /// </summary>
        private static IConfiguration LoadConfiguration() {
            var basePath = FindAppsettingsDirectory();
            if (basePath is null) {
                return new ConfigurationBuilder().Build();
            }

            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
        }

        /// <summary>
        /// 按优先级搜索包含 <c>appsettings.json</c> 的目录。
        /// </summary>
        private static string? FindAppsettingsDirectory() {
            var cwd = Directory.GetCurrentDirectory();

            // 优先级 1：当前目录直接包含 appsettings.json（从 Host 或解决方案根运行）
            if (File.Exists(Path.Combine(cwd, "appsettings.json"))) {
                return cwd;
            }

            // 优先级 2 & 3：向上遍历，寻找包含 Zeye.Sorting.Hub.Host/appsettings.json 的目录
            var dir = new DirectoryInfo(cwd);
            for (var i = 0; i < MaxParentDirectorySearchDepth && dir != null; i++) {
                var hostAppsettings = Path.Combine(dir.FullName, "Zeye.Sorting.Hub.Host", "appsettings.json");
                if (File.Exists(hostAppsettings)) {
                    return Path.Combine(dir.FullName, "Zeye.Sorting.Hub.Host");
                }

                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// 按优先级解析 MySQL 服务端版本，不主动锁定版本号：
        /// <list type="number">
        ///   <item><description>
        ///     <c>ServerVersion.AutoDetect</c> — 最优先；数据库可连通时自动探测，无任何版本限制。
        ///   </description></item>
        ///   <item><description>
        ///     兜底 MySQL 8.0 — 仅当 AutoDetect 失败时使用（设计时无数据库为正常场景）。
        ///     该值仅影响 <c>dotnet ef</c> 设计时模型分析，不影响运行时的 AutoDetect 行为。
        ///   </description></item>
        /// </list>
        /// </summary>
        /// <summary>
        /// 方法：ResolveServerVersion。
        /// </summary>
        private static ServerVersion ResolveServerVersion(string connectionString) {
            try {
                return ServerVersion.AutoDetect(connectionString);
            }
            catch {
                // 设计时无数据库连接属正常情况，静默降级到兜底版本
            }

            return new MySqlServerVersion(new Version(8, 0, 0));
        }
    }
}
