using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>LiteDB 唯一配置源，通知 options 管线；启动参数在重启前保持原有效值。</summary>
public sealed class RuntimeConfigurationProvider(LiteDbConfigurationStore store) : ConfigurationProvider, IConfigurationSource {
    /// <summary>保护当前配置与启动配置的一致性。</summary>
    private readonly object _gate = new();
    /// <summary>最后读取并通过校验的持久化配置。</summary>
    private JsonObject _stored = new();
    /// <summary>本次进程固定的启动配置。</summary>
    private Dictionary<string, string?> _startup = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>用于识别环境变量和命令行覆盖的配置根。</summary>
    private IConfigurationRoot? _configuration;
    /// <summary>框架 urls 键在宿主绑定前提供的显式监听地址覆盖。</summary>
    private string? _explicitHostingUrls;
    /// <summary>发布配置之前执行的宿主校验。</summary>
    public event Action<IConfiguration>? Validating;
    /// <summary>最近一次重载失败的诊断摘要。</summary>
    public string? LastReloadError { get; private set; }
    /// <summary>LiteDB 当前配置文件的位置。</summary>
    public string StoragePath => store.DatabasePath;
    /// <summary>提供 .NET 配置源的当前实例。</summary>
    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;
    /// <summary>绑定环境变量、用户密钥和命令行覆盖来源。</summary>
    public void UseOverrides(IConfigurationRoot configuration) { _configuration = configuration; _explicitHostingUrls = configuration["urls"]; }

    /// <summary>加载当前配置并固定本次启动参数。</summary>
    public override void Load() {
        lock (_gate) {
            _stored = store.ReadRuntime();
            Data = ConfigurationDocument.Flatten(_stored);
            _startup = Data.Where(x => RequiresRestart(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>识别需要在下次启动生效的参数。</summary>
    public static bool RequiresRestart(string key) {
        if (key.StartsWith("WebRequestAuditLog:Background", StringComparison.OrdinalIgnoreCase)
            || key.Equals("WebRequestAuditLog:DropLogIntervalSeconds", StringComparison.OrdinalIgnoreCase)
            || key.Equals("ResourceThresholds:MaxConnectionPoolSize", StringComparison.OrdinalIgnoreCase)) return true;
        return !new[] { "LogCleanup:", "ResourceThresholds:", "WebRequestAuditLog:", "Logging:", "Access:" }
            .Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>发布之前校验完整的有效配置。</summary>
    private void Validate(JsonObject values) {
        var data = ConfigurationDocument.Flatten(values);
        if (_configuration is not null) {
            var keys = data.Keys.Union(_configuration.AsEnumerable().Select(x => x.Key), StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var provider in _configuration.Providers.SkipWhile(x => !ReferenceEquals(x, this)).Skip(1))
                foreach (var key in keys) if (provider.TryGet(key, out var value)) data[key] = value;
        }
        using var root = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        Validating?.Invoke(root);
    }

    /// <summary>发布热更新值并保留当前启动参数。</summary>
    private void Publish(JsonObject value) {
        _stored = value;
        var data = ConfigurationDocument.Flatten(value);
        foreach (var key in data.Keys.Where(RequiresRestart).ToArray()) data.Remove(key);
        foreach (var pair in _startup) data[pair.Key] = pair.Value;
        Data = data;
    }

    /// <summary>为超级管理员配置入口捕获保存原值、生效原值和版本状态；返回独立快照。</summary>
    public RuntimeConfigurationState Capture() {
        lock (_gate) {
            var values = ConfigurationDocument.Flatten(_stored);
            var overrides = _configuration?.Providers.SkipWhile(x => !ReferenceEquals(x, this)).Skip(1).ToArray() ?? [];
            var overridden = values.Keys.Where(key => overrides.Any(provider => provider.TryGet(key, out _))).ToList();
            var restart = values.Keys.Union(_startup.Keys, StringComparer.OrdinalIgnoreCase).Where(RequiresRestart)
                .Where(key => values.GetValueOrDefault(key) != _startup.GetValueOrDefault(key)).ToArray();
            var effective = new JsonObject();
            foreach (var entry in Data) effective[entry.Key] = _configuration is null ? entry.Value : _configuration[entry.Key];
            // ASPNETCORE_URLS / --urls 以及宿主绑定使用框架的 urls 键，公开实际监听值。
            var hostingKey = values.Keys.FirstOrDefault(key => key.Equals("Hosting:Urls", StringComparison.OrdinalIgnoreCase));
            if (hostingKey is not null && !string.IsNullOrWhiteSpace(_configuration?["urls"])) {
                effective[hostingKey] = _configuration["urls"];
                if (!string.IsNullOrWhiteSpace(_explicitHostingUrls) || _configuration["urls"] != _startup.GetValueOrDefault(hostingKey)) overridden.Add(hostingKey);
            }
            return new((JsonObject)_stored.DeepClone(), effective, ConfigurationDocument.Revision(_stored), StoragePath,
                overridden.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), restart, values.Keys.Where(key => !RequiresRestart(key)).ToArray(), LastReloadError);
        }
    }

    /// <summary>校验版本和修改内容后持久化配置。</summary>
    public RuntimeConfigurationSaveResult Save(string revision, JsonObject patch) {
        string[] changed;
        string savedRevision;
        lock (_gate) {
            if (patch.Any(x => x.Key.Equals("ConfigurationStorage", StringComparison.OrdinalIgnoreCase) || x.Key.Equals("Kestrel", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("配置存储及 Kestrel 参数请在 appsettings.json 中修改并重启。");
            if (patch.Any(x => x.Key.Equals("FusionIngestion", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Fusion 接入配置请使用接入目录接口维护。");
            var current = store.ReadRuntime();
            if (revision != ConfigurationDocument.Revision(current)) throw new ConfigurationConflictException();
            ValidateShape(patch, current);
            var next = ConfigurationDocument.Merge(current, patch);
            try { Validate(next); }
            catch (Microsoft.Extensions.Options.OptionsValidationException exception) {
                throw new ArgumentException(string.Join("；", exception.Failures), exception);
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException) {
                throw new ArgumentException("配置类型或取值无效。", exception);
            }
            changed = ConfigurationDocument.ChangedKeys(current, next);
            if (!store.WriteRuntime(revision, next)) throw new ConfigurationConflictException();
            Publish(next); LastReloadError = null;
            savedRevision = ConfigurationDocument.Revision(next);
        }
        if (changed.Any(key => !RequiresRestart(key))) OnReload();
        return new(savedRevision, changed, changed.Where(RequiresRestart).ToArray());
    }

    /// <summary>拒绝未知字段、空值和错误的配置结构。</summary>
    private static void ValidateShape(JsonObject patch, JsonObject schema, string path = "") {
        foreach (var pair in patch) {
            var original = schema.FirstOrDefault(x => x.Key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            var key = path + pair.Key;
            if (original.Key is null && !path.Equals("Logging:LogLevel:", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(key + " 不是可编辑字段。");
            if (pair.Value is null) throw new ArgumentException(key + " 不能为 null。");
            if (original.Value is JsonObject obj) {
                if (pair.Value is not JsonObject nested) throw new ArgumentException(key + " 必须为对象。");
                ValidateShape(nested, obj, key + ":");
            } else if (original.Value is JsonArray) {
                if (pair.Value is not JsonArray) throw new ArgumentException(key + " 必须为数组。");
            } else if (pair.Value is JsonObject or JsonArray) throw new ArgumentException(key + " 字段类型无效。");
            else if (original.Value is JsonValue scalar && pair.Value is JsonValue next) {
                if (scalar.TryGetValue<bool>(out _) && !next.TryGetValue<bool>(out _)
                    || scalar.TryGetValue<decimal>(out _) && !next.TryGetValue<decimal>(out _)
                    || scalar.TryGetValue<string>(out _) && !next.TryGetValue<string>(out _)) throw new ArgumentException(key + " 字段类型无效。");
            }
        }
    }

    /// <summary>检测外部配置变更，失败时保留有效快照。</summary>
    public bool TryReload() {
        bool notify;
        lock (_gate) {
            try {
                var next = store.ReadRuntime();
                if (ConfigurationDocument.Revision(next) == ConfigurationDocument.Revision(_stored)) { LastReloadError = null; return false; }
                Validate(next);
                notify = ConfigurationDocument.ChangedKeys(_stored, next).Any(key => !RequiresRestart(key));
                Publish(next); LastReloadError = null;
            } catch (Exception exception) {
                LastReloadError = "外部配置读取或校验失败，保留最后有效配置。";
                NLog.LogManager.GetCurrentClassLogger().Warn(exception, LastReloadError);
                return false;
            }
        }
        if (notify) OnReload();
        return true;
    }
}
