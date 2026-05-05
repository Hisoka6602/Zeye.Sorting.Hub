using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime {

    /// <summary>
    /// 设计时配置目录定位器，为 dotnet ef 工具链提供统一的 appsettings.json 查找与加载逻辑。
    /// </summary>
    /// <remarks>
    /// 按以下优先级搜索包含 appsettings.json 的目录：
    /// <list type="number">
    ///   <item><description>当前工作目录（从 Host 或解决方案根目录运行时有效）</description></item>
    ///   <item><description>向上遍历父目录，查找包含 Zeye.Sorting.Hub.Host/appsettings.json 的父目录</description></item>
    /// </list>
    /// </remarks>
    internal static class DesignTimeConfigurationLocator {

        /// <summary>
        /// 向上遍历父目录时的最大层级数，防止无限递归到文件系统根目录。
        /// </summary>
        private const int MaxParentDirectorySearchDepth = 6;

        /// <summary>
        /// 从 appsettings.json 加载设计时配置。
        /// 按优先级搜索以下路径：当前工作目录 → 向上遍历父目录中的 Host 子目录。
        /// 同时叠加进程环境变量中使用 <c>__</c> 分隔的配置键，便于 CI 在不改写示例配置的前提下覆盖连接字符串。
        /// </summary>
        /// <returns>加载的配置对象；若未找到 appsettings.json 则返回仅包含环境变量覆盖项的空配置。</returns>
        public static IConfiguration LoadConfiguration() {
            var builder = new ConfigurationBuilder();
            var basePath = FindAppsettingsDirectory();
            if (basePath is not null) {
                builder
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Development.json", optional: true);
            }

            AppendEnvironmentVariableOverrides(builder);
            return builder.Build();
        }

        /// <summary>
        /// 将环境变量中的双下划线配置键追加为配置覆盖项。
        /// </summary>
        /// <param name="builder">配置构建器。</param>
        private static void AppendEnvironmentVariableOverrides(IConfigurationBuilder builder) {
            var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (string key in environmentVariables.Keys) {
                if (!key.Contains("__", StringComparison.Ordinal)) {
                    continue;
                }

                var normalizedKey = key.Replace("__", ":", StringComparison.Ordinal);
                overrides[normalizedKey] = environmentVariables[key]?.ToString();
            }

            if (overrides.Count > 0) {
                builder.AddInMemoryCollection(overrides);
            }
        }

        /// <summary>
        /// 按优先级搜索包含 appsettings.json 的目录。
        /// </summary>
        /// <returns>包含 appsettings.json 的目录路径；未找到时返回 null。</returns>
        private static string? FindAppsettingsDirectory() {
            var cwd = Directory.GetCurrentDirectory();

            // 优先级 1：当前目录直接包含 appsettings.json（从 Host 或解决方案根运行）
            if (File.Exists(Path.Combine(cwd, "appsettings.json"))) {
                return cwd;
            }

            // 优先级 2：向上遍历，寻找包含 Zeye.Sorting.Hub.Host/appsettings.json 的父目录
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
    }
}
