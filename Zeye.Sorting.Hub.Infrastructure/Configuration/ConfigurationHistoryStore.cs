using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Configuration;

/// <summary>使用 EF Core 读写 SQLite 配置变更历史，与 LiteDB 中的当前配置分开保存。</summary>
public sealed class ConfigurationHistoryStore {
    /// <summary>SQLite 历史库连接参数。</summary>
    private readonly string _connectionString;
    /// <summary>不可变的 EF Core 配置；每次操作使用独立上下文。</summary>
    private DbContextOptions<ConfigurationHistoryDbContext> _options;
    /// <summary>配置加载完成后绑定 SQLite 历史业务读写诊断，初始化引导不形成依赖环。</summary>
    public void AttachQueryDiagnostics(SlowQueryAutoTuningPipeline pipeline) => _options =
        new DbContextOptionsBuilder<ConfigurationHistoryDbContext>().UseSqlite(_connectionString)
            .AddInterceptors(new SlowQueryCommandInterceptor(pipeline, databaseRole: "configuration"),
                new SlowQueryConnectionInterceptor(pipeline), new SlowQueryTransactionInterceptor(pipeline, "configuration")).Options;
    /// <summary>当前使用的数据库绝对路径。</summary>
    public string DatabasePath { get; }

    /// <summary>自动建立 SQLite 历史文件、表及索引。</summary>
    public ConfigurationHistoryStore(string path) {
        DatabasePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, DefaultTimeout = 10, Pooling = false }.ToString();
        _options = new DbContextOptionsBuilder<ConfigurationHistoryDbContext>().UseSqlite(_connectionString).Options;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 1) throw new InvalidDataException("配置历史库版本高于当前程序，不能降级打开。");
        // 原生命令只用于既有版本一文件的幂等建表、WAL 和版本标记；业务读写统一使用 EF Core。
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS ConfigurationChanges (
                Id TEXT PRIMARY KEY, DocumentKey TEXT NOT NULL, PreviousRevision TEXT NOT NULL,
                Revision TEXT NOT NULL, BeforeJson TEXT NOT NULL, AfterJson TEXT NOT NULL,
                ChangedKeys TEXT NOT NULL, RecordedAtLocal TEXT NOT NULL, Status TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS IX_ConfigurationChanges_Key_Time ON ConfigurationChanges(DocumentKey, RecordedAtLocal);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>打开具有超时限制的独立 SQLite 连接。</summary>
    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }

    /// <summary>先耐久记录修改前后原值与待确认状态，仅通过超级管理员入口查询。</summary>
    public string Prepare(string key, string previousRevision, string revision, JsonNode before, JsonNode after) {
        var id = Guid.NewGuid().ToString("N");
        using var db = new ConfigurationHistoryDbContext(_options);
        db.Add(new ConfigurationHistoryRecord {
            ChangeKey = id, DocumentKey = key, PreviousRevision = previousRevision, Revision = revision,
            BeforeJson = before.ToJsonString(), AfterJson = after.ToJsonString(),
            ChangedKeysJson = JsonSerializer.Serialize(ConfigurationDocument.ChangedKeys(before, after)),
            RecordedAtLocal = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture), Status = "Pending"
        });
        try { db.SaveChanges(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException storageException) {
            // 保持现有 API 的存储失败处理和配置事务回滚契约。
            ExceptionDispatchInfo.Capture(storageException).Throw();
        }
        return id;
    }

    /// <summary>确认当前变更是否已经提交。</summary>
    public void Complete(string id, bool committed) => SetStatus(id, committed ? "Committed" : "Failed");

    /// <summary>保存确认状态；无法从较新版本证明结果时保留未确认状态。</summary>
    private void SetStatus(string id, string status) {
        using var db = new ConfigurationHistoryDbContext(_options);
        db.Set<ConfigurationHistoryRecord>().Where(row => row.ChangeKey == id)
            .ExecuteUpdate(setters => setters.SetProperty(row => row.Status, status));
    }

    /// <summary>读取当前配置文档或最近的耐久历史。</summary>
    public ConfigurationHistoryEntry[] Read(int limit = 100) {
        using var db = new ConfigurationHistoryDbContext(_options);
        var rows = db.Set<ConfigurationHistoryRecord>().AsNoTracking().OrderByDescending(row => row.RecordedAtLocal)
            .ThenByDescending(row => EF.Property<long>(row, "Sequence")).Take(Math.Clamp(limit, 1, 500)).ToArray();
        return rows.Select(row => new ConfigurationHistoryEntry(row.ChangeKey, row.DocumentKey, row.PreviousRevision, row.Revision,
            row.BeforeJson, row.AfterJson, JsonSerializer.Deserialize<string[]>(row.ChangedKeysJson)!, row.RecordedAtLocal, row.Status)).ToArray();
    }

    /// <summary>进程在两个存储提交之间退出时，通过耐久版本恢复历史状态。</summary>
    public void Recover(Func<string, string?> revision) {
        using var db = new ConfigurationHistoryDbContext(_options);
        var pending = db.Set<ConfigurationHistoryRecord>().AsNoTracking().Where(row => row.Status == "Pending")
            .Select(row => new { row.ChangeKey, row.DocumentKey, row.PreviousRevision, row.Revision }).ToArray();
        foreach (var entry in pending) {
            var current = revision(entry.DocumentKey);
            SetStatus(entry.ChangeKey, current == entry.Revision ? "Committed" : current == entry.PreviousRevision ? "Failed" : "Unconfirmed");
        }
    }
}
