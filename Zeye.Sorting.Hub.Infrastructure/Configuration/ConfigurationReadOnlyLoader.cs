using System.Text.Json.Nodes;
using LiteDB;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>设计时工具读取已有权威配置，不建立配置库或写入历史。</summary>
public static class ConfigurationReadOnlyLoader {
    /// <summary>优先读取 LiteDB 快照；首次部署之前兼容读取 JSON 和内置默认值。</summary>
    public static JsonObject Load(string contentRoot, string environment = "Production") {
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("ZEYE_HUB_CONFIG_ROOT") ?? contentRoot);
        var legacy = ReadLegacy(root, environment);
        var storage = ConfigurationDocument.Flatten(legacy).GetValueOrDefault("ConfigurationStorage:LiteDbPath");
        var databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("ConfigurationStorage__LiteDbPath") ?? storage ?? "data/configuration/settings.db", root);
        var defaults = ConfigurationDocument.Defaults(environment);
        var current = ConfigurationDocument.Merge(defaults, legacy);
        if (File.Exists(databasePath)) {
            using var database = new LiteDatabase(new ConnectionString { Filename = databasePath, Connection = ConnectionType.Shared, ReadOnly = true });
            var record = database.GetCollection<BsonDocument>("runtime_configuration").FindById("current");
            if (record is not null) {
                current = ConfigurationDocument.CompleteDatabaseDefaults(ConfigurationDocument.Parse(record["json"].AsString), defaults);
                foreach (var section in new[] { "ConfigurationStorage", "Kestrel" }) if (legacy[section] is not null) current[section] = legacy[section]!.DeepClone();
            }
        }
        return current;
    }
    /// <summary>统一合并旧主配置和旧环境配置；缺失文件使用空对象。</summary>
    public static JsonObject ReadLegacy(string root, string environment) {
        var result = new JsonObject();
        foreach (var file in new[] { "appsettings.json", $"appsettings.{environment}.json" }.Distinct()) {
            var path = Path.Combine(root, file);
            if (File.Exists(path)) result = ConfigurationDocument.Merge(result, ConfigurationDocument.Parse(File.ReadAllText(path)));
        }
        return result;
    }
}
