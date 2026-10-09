using System.Text.Json.Nodes;
using LiteDB;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>配置文件、集合和版本的幂等初始化；共享连接事务保护跨进程版本校验。</summary>
public sealed class LiteDbConfigurationStore : IConfigurationDocumentStore, IDisposable {
    /// <summary>当前程序支持的配置集合结构版本。</summary>
    public const int SchemaVersion = 1;
    /// <summary>序列化同一实例内的配置事务。</summary>
    private readonly object _gate = new();
    /// <summary>共享模式的 LiteDB 配置数据库。</summary>
    private readonly LiteDatabase _database;
    /// <summary>配置变更的 SQLite 历史存储。</summary>
    private readonly ConfigurationHistoryStore _history;
    /// <summary>整个运行配置的当前快照集合。</summary>
    private readonly ILiteCollection<BsonDocument> _runtime;
    /// <summary>规则、运维策略和接入目录配置集合。</summary>
    private readonly ILiteCollection<BsonDocument> _documents;
    /// <summary>配置事务与历史恢复异常日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>当前使用的数据库绝对路径。</summary>
    public string DatabasePath { get; }
    /// <summary>与当前配置分开保存的变更历史。</summary>
    public ConfigurationHistoryStore History => _history;

    /// <summary>初始化配置文件和集合，兼容导入旧快照。</summary>
    public LiteDbConfigurationStore(string path, ConfigurationHistoryStore history, JsonObject defaults, JsonObject legacy) {
        DatabasePath = Path.GetFullPath(path); _history = history;
        if (DatabasePath.Equals(history.DatabasePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("配置库与历史库不能使用同一个文件。");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _database = new LiteDatabase(new ConnectionString { Filename = DatabasePath, Connection = ConnectionType.Shared });
        _runtime = _database.GetCollection<BsonDocument>("runtime_configuration");
        _documents = _database.GetCollection<BsonDocument>("configuration_documents");
        try {
            Transaction(() => {
                var metadata = _database.GetCollection<BsonDocument>("configuration_schema");
                var schema = metadata.FindById("current");
                var version = schema?["version"].AsInt32 ?? 0;
                if (version > SchemaVersion) throw new InvalidDataException("LiteDB 配置版本高于当前程序，不能降级打开。");
                var current = _runtime.FindById("current");
                if (current is null) {
                    var initial = ConfigurationDocument.Merge(defaults, legacy);
                    foreach (var key in initial.Select(x => x.Key).Where(IsBootstrap).ToArray()) initial.Remove(key);
                    _runtime.Insert(RuntimeRecord(initial, 1));
                } else if (version < SchemaVersion) {
                    // 兼容旧版只有 json/updatedAt 的 runtime_configuration 快照。
                    var migrated = ConfigurationDocument.Merge(defaults, ConfigurationDocument.Parse(current["json"].AsString));
                    foreach (var key in migrated.Select(x => x.Key).Where(IsBootstrap).ToArray()) migrated.Remove(key);
                    _runtime.Upsert(RuntimeRecord(migrated, current["revision"].IsInt32 ? current["revision"].AsInt32 + 1 : 1));
                } else {
                    // 存储结构版本未变时，新数据库提供器的字段仍需补齐；既有连接和其他设置保持权威。
                    var before = ConfigurationDocument.Parse(current["json"].AsString);
                    var completed = ConfigurationDocument.CompleteDatabaseDefaults(before, defaults);
                    var previous = ConfigurationDocument.Revision(before);
                    var revision = ConfigurationDocument.Revision(completed);
                    if (previous != revision) {
                        AuditWrite("runtime", previous, revision, before, completed,
                            () => _runtime.Upsert(RuntimeRecord(completed, checked(current["revision"].AsInt32 + 1))));
                    }
                }
                _documents.EnsureIndex("revision");
                metadata.Upsert(new BsonDocument { ["_id"] = "current", ["version"] = SchemaVersion });
                return true;
            });
            _history.Recover(key => key == "runtime" ? ConfigurationDocument.Revision(ReadRuntime()) : Read(key)?.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
        } catch { _database.Dispose(); throw; }
    }

    /// <summary>在同一 LiteDB 事务中完成版本检查和配置保存。</summary>
    private T Transaction<T>(Func<T> action) {
        lock (_gate) {
            _database.BeginTrans();
            try {
                var result = action(); _database.Commit();
                CompleteAudits(true);
                return result;
            }
            catch { _database.Rollback(); CompleteAudits(false); throw; }
        }
    }

    /// <summary>创建当前运行配置的 BSON 存储记录。</summary>
    private static BsonDocument RuntimeRecord(JsonObject value, int revision) => new() {
        ["_id"] = "current", ["json"] = value.ToJsonString(), ["revision"] = revision, ["updatedAt"] = DateTime.Now
    };
    /// <summary>读取 LiteDB 中的当前运行配置。</summary>
    public JsonObject ReadRuntime() { lock (_gate) return ConfigurationDocument.Parse(_runtime.FindById("current")["json"].AsString); }

    /// <summary>按内容版本原子保存运行配置。</summary>
    public bool WriteRuntime(string expectedRevision, JsonObject next) => Transaction(() => {
        var current = _runtime.FindById("current"); var before = ConfigurationDocument.Parse(current["json"].AsString);
        if (expectedRevision != ConfigurationDocument.Revision(before)) return false;
        if (ConfigurationDocument.Revision(before) == ConfigurationDocument.Revision(next)) return true;
        AuditWrite("runtime", expectedRevision, ConfigurationDocument.Revision(next), before, next,
            () => _runtime.Upsert(RuntimeRecord(next, checked(current["revision"].AsInt32 + 1))));
        return true;
    });

    /// <summary>读取当前配置文档或最近的耐久历史。</summary>
    public ManagedDocument? Read(string key) {
        RequireConfiguration(key);
        lock (_gate) { var record = _documents.FindById(key); return record is null ? null : FromRecord(record); }
    }
    /// <summary>转换 BSON 当前配置为管理文档合同。</summary>
    private static ManagedDocument FromRecord(BsonDocument record) => new() {
        Key = record["_id"].AsString, Json = record["json"].AsString,
        Revision = record["revision"].AsInt32, ModifiedAt = record["modifiedAt"].AsDateTime
    };
    /// <summary>转换管理文档为 BSON 当前配置。</summary>
    private static BsonDocument ToRecord(ManagedDocument value) => new() {
        ["_id"] = value.Key, ["json"] = value.Json, ["revision"] = value.Revision, ["modifiedAt"] = value.ModifiedAt
    };

    /// <summary>按预期版本保存单个配置文档。</summary>
    public ManagedDocument? Write(string key, string json, int expectedRevision) {
        RequireConfiguration(key);
        var after = JsonNode.Parse(json) ?? throw new System.Text.Json.JsonException("配置文档不能为空。");
        return Transaction(() => {
            var record = _documents.FindById(key);
            if ((record?["revision"].AsInt32 ?? 0) != expectedRevision) return null;
            var saved = new ManagedDocument { Key = key, Json = json, Revision = checked(expectedRevision + 1), ModifiedAt = DateTime.Now };
            AuditWrite(key, expectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), saved.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), record is null ? new JsonObject() : JsonNode.Parse(record["json"].AsString)!, after,
                () => _documents.Upsert(ToRecord(saved)));
            return saved;
        });
    }

    /// <summary>保留旧版文档版本及加密密钥；已有 LiteDB 配置永不被旧数据库覆盖。</summary>
    public void Import(IEnumerable<ManagedDocument> documents) {
        var batch = documents.ToArray();
        foreach (var document in batch) {
            RequireConfiguration(document.Key);
            if (document.Revision < 1 || JsonNode.Parse(document.Json) is null) throw new InvalidDataException("旧配置文档版本或格式无效。");
        }
        Transaction(() => {
            foreach (var document in batch) if (_documents.FindById(document.Key) is null) _documents.Insert(ToRecord(document));
            return true;
        });
    }

    /// <summary>在配置提交前准备独立的变更原值历史。</summary>
    private void AuditWrite(string key, string previous, string revision, JsonNode before, JsonNode after, Action write) {
        var id = _history.Prepare(key, previous, revision, before, after);
        // 状态在 LiteDB 事务完成之后才确认；崩溃后的 Pending 由启动恢复核对耐久版本。
        _pendingAudits.Add(id);
        write();
    }
    /// <summary>当前事务需要确认的历史编号。</summary>
    private readonly List<string> _pendingAudits = [];
    /// <summary>提交后确认历史状态，失败时留待恢复。</summary>
    private void CompleteAudits(bool committed) {
        foreach (var id in _pendingAudits) {
            try { _history.Complete(id, committed); }
            catch (Exception exception) { Logger.Error(exception, "配置历史提交状态暂未确认，将在启动时恢复，Id={Id}", id); }
        }
        _pendingAudits.Clear();
    }
    /// <summary>阻止业务和历史文档进入 LiteDB 配置集合。</summary>
    private static void RequireConfiguration(string key) {
        if (!IConfigurationDocumentStore.IsConfiguration(key)) throw new ArgumentException("业务或历史文档不能写入配置库。", nameof(key));
    }
    /// <summary>仅保留在 JSON 启动入口中的配置节。</summary>
    private static bool IsBootstrap(string key) => key.Equals("ConfigurationStorage", StringComparison.OrdinalIgnoreCase) || key.Equals("Kestrel", StringComparison.OrdinalIgnoreCase);
    /// <summary>释放当前配置数据库连接。</summary>
    public void Dispose() { lock (_gate) _database.Dispose(); }
}
