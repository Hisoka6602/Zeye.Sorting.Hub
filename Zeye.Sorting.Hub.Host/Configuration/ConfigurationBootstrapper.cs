using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Configuration;

/// <summary>在监听地址、日志和 EF 工厂绑定之前建立 LiteDB 配置源。</summary>
public static class ConfigurationBootstrapper {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedConfigurationStorageKestrelValues = new[] { "ConfigurationStorage", "Kestrel" };

    /// <summary>在其他宿主服务绑定前初始化配置存储。</summary>
    public static void Configure(WebApplicationBuilder builder) {
        var configuration = builder.Configuration;
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("ZEYE_HUB_CONFIG_ROOT") ?? builder.Environment.ContentRootPath);
        var defaults = ConfigurationDocument.Defaults(builder.Environment.EnvironmentName);
        var legacy = ConfigurationReadOnlyLoader.ReadLegacy(root, builder.Environment.EnvironmentName);
        var bootstrap = ConfigurationDocument.Flatten(legacy).Where(x => IsBootstrap(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var sources = configuration.Sources.ToArray();
        var providers = ((IConfigurationRoot)configuration).Providers.ToArray();
        var overrides = sources.Where(IsOverride).ToArray();
        var bootstrapKeys = bootstrap.Keys.Union(configuration.AsEnumerable().Select(x => x.Key).Where(IsBootstrap), StringComparer.OrdinalIgnoreCase).ToArray();
        for (var i = 0; i < sources.Length; i++) {
            if (!IsOverride(sources[i])) continue;
            foreach (var key in bootstrapKeys) if (providers[i].TryGet(key, out var value)) bootstrap[key] = value;
        }
        var path = Path.GetFullPath(bootstrap.GetValueOrDefault("ConfigurationStorage:LiteDbPath") ?? "data/configuration/settings.db", root);
        var historyPath = Path.GetFullPath(bootstrap.GetValueOrDefault("ConfigurationStorage:HistorySqlitePath") ?? "data/business-history/configuration-history.db", root);
        if (path.Equals(historyPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("LiteDB 配置文件与 SQLite 历史文件必须分开。");
        foreach (var key in legacy.Select(x => x.Key).Where(IsBootstrap).ToArray()) legacy.Remove(key);
        foreach (var key in defaults.Select(x => x.Key).Where(IsBootstrap).ToArray()) defaults.Remove(key);
        bootstrap["ConfigurationStorage:Provider"] = "LiteDB";
        var history = new ConfigurationHistoryStore(historyPath);
        var store = new LiteDbConfigurationStore(path, history, defaults, legacy);
        var source = new RuntimeConfigurationProvider(store);
        source.Validating += HostConfigurationValidator.Validate;
        configuration.Sources.Clear();
        configuration.Sources.Add(source);
        configuration.AddInMemoryCollection(bootstrap);
        foreach (var item in overrides) configuration.Sources.Add(item);
        source.UseOverrides(configuration);
        HostConfigurationValidator.Validate(configuration);
        builder.Services.AddSingleton(_ => store);
        builder.Services.AddSingleton<IConfigurationDocumentStore>(p => p.GetRequiredService<LiteDbConfigurationStore>());
        builder.Services.AddSingleton(history);
        builder.Services.AddSingleton(source);
        builder.Services.AddSingleton<IOptions<ResourceThresholdsOptions>, ReloadableOptions<ResourceThresholdsOptions>>();
        builder.Services.AddSingleton<IOptions<WebRequestAuditLogOptions>, ReloadableOptions<WebRequestAuditLogOptions>>();
        builder.Services.AddHostedService<ConfigurationReloadHostedService>();
    }
    /// <summary>配置位置和 Kestrel 证书属于 JSON 启动配置。</summary>
    private static bool IsBootstrap(string key) => CachedConfigurationStorageKestrelValues
        .Any(section => key.Equals(section, StringComparison.OrdinalIgnoreCase) || key.StartsWith(section + ":", StringComparison.OrdinalIgnoreCase));
    /// <summary>保留环境变量、命令行和用户密钥；旧 appsettings 只作为首次导入来源。</summary>
    private static bool IsOverride(IConfigurationSource source) => source is not JsonConfigurationSource json
        || !(json.Path?.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ?? false);
}
