using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>配置快照、兼容合并和内容版本。数组始终整体替换。</summary>
public static class ConfigurationDocument {
    /// <summary>读取支持注释和尾逗号的旧版配置 JSON。</summary>
    public static JsonObject Parse(string text) => JsonNode.Parse(text, documentOptions: new() {
        AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip
    }) as JsonObject ?? throw new JsonException("配置必须为 JSON 对象。");

    /// <summary>加载程序内置默认配置与环境默认值。</summary>
    public static JsonObject Defaults(string? environment = null) {
        using var stream = typeof(ConfigurationDocument).Assembly.GetManifestResourceStream(
            "Zeye.Sorting.Hub.Infrastructure.Configuration.default-settings.json")
            ?? throw new InvalidOperationException("缺少内置配置默认值。");
        using var reader = new StreamReader(stream);
        var result = Parse(reader.ReadToEnd());
        using var overlay = typeof(ConfigurationDocument).Assembly.GetManifestResourceStream(
            $"Zeye.Sorting.Hub.Infrastructure.Configuration.default-settings.{environment}.json");
        if (overlay is not null) { using var overlayReader = new StreamReader(overlay); result = Merge(result, Parse(overlayReader.ReadToEnd())); }
        return result;
    }

    /// <summary>按字段兼容合并配置，对数组整体替换。</summary>
    public static JsonObject Merge(JsonObject source, JsonObject patch) {
        var result = (JsonObject)source.DeepClone();
        foreach (var item in patch) {
            var key = result.Select(x => x.Key).FirstOrDefault(x => x.Equals(item.Key, StringComparison.OrdinalIgnoreCase)) ?? item.Key;
            result[key] = result[key] is JsonObject before && item.Value is JsonObject after
                ? Merge(before, after) : item.Value?.DeepClone();
        }
        return result;
    }

    /// <summary>仅补齐新增的数据库配置字段；已有值（包括空值）优先，不重新导入其他配置节或旧 JSON。</summary>
    public static JsonObject CompleteDatabaseDefaults(JsonObject configuration, JsonObject defaults) {
        var databaseDefaults = new JsonObject();
        if (defaults["ConnectionStrings"] is JsonObject connections)
            databaseDefaults["ConnectionStrings"] = connections.DeepClone();
        if (defaults["Persistence"] is JsonObject persistence && persistence.ContainsKey("Provider"))
            databaseDefaults["Persistence"] = new JsonObject { ["Provider"] = persistence["Provider"]?.DeepClone() };
        return Merge(databaseDefaults, configuration);
    }

    /// <summary>将配置转换为 .NET 使用的冒号分隔键。</summary>
    public static Dictionary<string, string?> Flatten(JsonNode? node) {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Walk(node, "");
        return values;
        void Walk(JsonNode? value, string path) {
            if (value is JsonObject obj) {
                if (obj.Count == 0 && path.Length > 0) values[path] = null;
                foreach (var pair in obj) Walk(pair.Value, path.Length == 0 ? pair.Key : path + ":" + pair.Key);
            } else if (value is JsonArray array) {
                if (array.Count == 0) values[path] = null;
                for (var i = 0; i < array.Count; i++) Walk(array[i], path + ":" + i.ToString(CultureInfo.InvariantCulture));
            } else if (path.Length > 0) values[path] = value is null ? null
                : value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : value.ToJsonString();
        }
    }

    /// <summary>对稳定排序的配置值生成内容版本。</summary>
    public static string Revision(JsonNode node) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        string.Join('\n', Flatten(node).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key.ToUpperInvariant() + "=" + JsonSerializer.Serialize(x.Value))))));

    /// <summary>比较两个快照并返回变化字段。</summary>
    public static string[] ChangedKeys(JsonNode before, JsonNode after) {
        var old = Flatten(before); var next = Flatten(after);
        return old.Keys.Union(next.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(key => old.ContainsKey(key) != next.ContainsKey(key) || old.GetValueOrDefault(key) != next.GetValueOrDefault(key))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

}
