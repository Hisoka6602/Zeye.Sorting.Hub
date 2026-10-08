using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using NLog;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

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
    /// 默认按 <c>Persistence:Provider</c> 解析数据库提供器（MySql / SqlServer / Oracle / SQLite），也支持通过
    /// <c>dotnet ef ... -- --provider SqlServer</c> 显式覆盖提供器。
    /// </para>
    /// <para>
    /// 连接字符串由只读配置加载器读取配置库，兼容尚未导入的 JSON，环境变量优先。
    /// 其中 provider 值与 ConnectionStrings key 均使用 <see cref="ConfiguredProviderNames"/> 常量。
    /// 工厂按以下顺序搜索 <c>appsettings.json</c>：
    /// <list type="number">
    ///   <item><description>当前工作目录（适用于从 Host 或解决方案根目录运行 <c>dotnet ef</c>）</description></item>
    ///   <item><description>当前工作目录的相邻 <c>Zeye.Sorting.Hub.Host</c> 子目录（适用于从 Infrastructure 目录运行）</description></item>
    ///   <item><description>向上遍历父目录寻找 <c>Zeye.Sorting.Hub.Host</c> 子目录</description></item>
    ///   <item><description>以上均未找到时使用无凭据的设计时占位连接字符串（仅用于模型分析，不应执行数据库更新）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal sealed class MySqlContextFactory : IDesignTimeDbContextFactory<SortingHubDbContext> {
        /// <summary>
        /// 数据库提供程序命令行参数名称。
        /// </summary>
        private const string ProviderArgumentName = "--provider";

        /// <summary>
        /// NLog 日志器，用于设计时警告落盘。
        /// </summary>
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 设计时兜底占位连接字符串，仅在无法从 <c>appsettings.json</c> 读取时使用。
        /// 使用不存在的专用库及身份，避免设计时工具误连业务库；数据库更新必须显式配置连接。
        /// </summary>
        private const string FallbackConnectionString =
            "server=127.0.0.1;port=3306;database=design_time_only;user=design_time_only;SslMode=None;";

        /// <inheritdoc />
        public SortingHubDbContext CreateDbContext(string[] args) {
            var config = DesignTimeConfigurationLocator.LoadConfiguration();
            return CreateDbContext(config, args);
        }

        /// <summary>根据给定配置构建设计时上下文，便于离线验证与运行期采用相同的索引生成策略。</summary>
        internal SortingHubDbContext CreateDbContext(IConfiguration config, params string[] args) {
            var provider = ResolveProvider(args, config);

            if (string.Equals(provider, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
                var factory = new SqlServerContextFactory();
                return factory.CreateDbContext(config);
            }

            if (provider is ConfiguredProviderNames.Oracle or ConfiguredProviderNames.SQLite) {
                var additionalConnection = config.GetConnectionString(provider);
                if (string.IsNullOrWhiteSpace(additionalConnection)) additionalConnection = provider == ConfiguredProviderNames.Oracle
                    ? "User Id=design_time_only;Password=design_time_only;Data Source=127.0.0.1:1521/FREEPDB1"
                    : "Data Source=data/business/design-time-only.db";
                var additionalOptions = new DbContextOptionsBuilder<SortingHubDbContext>();
                AdditionalDbContextOptions.Configure(additionalOptions, provider, additionalConnection, DesignTimeConfigurationLocator.ResolveContentRoot());
                return new SortingHubDbContext(additionalOptions.Options);
            }

            var connectionString = config.GetConnectionString(ConfiguredProviderNames.MySql);
            var normalizedConnectionString = string.IsNullOrWhiteSpace(connectionString)
                ? FallbackConnectionString
                : connectionString.Trim();
            var serverVersion = DependencyInjection.PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
                config,
                normalizedConnectionString,
                msg => Logger.Warn("[DesignTime] {Message}", msg));
            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(normalizedConnectionString, serverVersion)
                .ReplaceService<IMigrationsSqlGenerator, MySqlOnlineIndexMigrationsSqlGenerator>()
                .Options;
            return new SortingHubDbContext(options);
        }

        /// <summary>
        /// 从命令行参数或配置中解析数据库提供器名称。
        /// </summary>
        private static string ResolveProvider(string[] args, IConfiguration config) {
            for (var i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (string.Equals(arg, ProviderArgumentName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) {
                    return NormalizeProvider(args[i + 1]);
                }

                if (arg.StartsWith($"{ProviderArgumentName}=", StringComparison.OrdinalIgnoreCase)) {
                    if (arg.Length == ProviderArgumentName.Length + 1) {
                        throw new InvalidOperationException("参数 '--provider=' 未提供值。可选值：MySql / SqlServer / Oracle / SQLite。");
                    }

                    var provided = arg[(ProviderArgumentName.Length + 1)..];
                    return NormalizeProvider(provided);
                }
            }

            return NormalizeProvider(config["Persistence:Provider"] ?? ConfiguredProviderNames.MySql);
        }

        /// <summary>
        /// 标准化并校验数据库提供器名称。
        /// </summary>
        private static string NormalizeProvider(string? provider) {
            return ConfiguredProviderNames.Normalize(provider);
        }

    }
}
