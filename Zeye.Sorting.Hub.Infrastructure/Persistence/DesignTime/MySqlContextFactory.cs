using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NLog;
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
    /// 其中 provider 值与 ConnectionStrings key 均使用 <see cref="ConfiguredProviderNames"/> 常量。
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
        private const string ProviderArgumentName = "--provider";

        /// <summary>
        /// NLog 日志器，用于设计时警告落盘。
        /// </summary>
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 设计时兜底占位连接字符串，仅在无法从 <c>appsettings.json</c> 读取时使用。
        /// 此值仅用于 <c>dotnet ef</c> 工具链的设计时模型分析，不影响运行时连接。
        /// </summary>
        private const string FallbackConnectionString =
            "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid=root;pwd=Admin@1234;SslMode=None;";

        /// <inheritdoc />
        public SortingHubDbContext CreateDbContext(string[] args) {
            var config = DesignTimeConfigurationLocator.LoadConfiguration();
            var provider = ResolveProvider(args, config);

            if (string.Equals(provider, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
                var factory = new SqlServerContextFactory();
                return factory.CreateDbContext(config);
            }

            var connectionString = config.GetConnectionString(ConfiguredProviderNames.MySql);
            var normalizedConnectionString = string.IsNullOrWhiteSpace(connectionString)
                ? FallbackConnectionString
                : connectionString.Trim();
            var serverVersion = DependencyInjection.PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
                config,
                normalizedConnectionString,
                msg => Logger.Warn("[DesignTime] {0}", msg));
            var options = new DbContextOptionsBuilder<SortingHubDbContext>()
                .UseMySql(normalizedConnectionString, serverVersion)
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
                        throw new InvalidOperationException($"参数 '--provider=' 未提供值。可选值：{ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}。");
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
            if (string.IsNullOrWhiteSpace(provider)) {
                throw new InvalidOperationException($"数据库提供器不能为空。可选值：{ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}。");
            }

            if (string.Equals(provider, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
                return ConfiguredProviderNames.SqlServer;
            }

            if (string.Equals(provider, ConfiguredProviderNames.MySql, StringComparison.OrdinalIgnoreCase)) {
                return ConfiguredProviderNames.MySql;
            }

            throw new InvalidOperationException($"不支持的数据库提供器：{provider}。可选值：{ConfiguredProviderNames.MySql} / {ConfiguredProviderNames.SqlServer}。");
        }

    }
}
