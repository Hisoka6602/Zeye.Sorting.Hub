using Microsoft.Data.Sqlite;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证 EF Core 历史读写兼容版本一文件及 SQLite 隐式行号。</summary>
public sealed class ConfigurationHistoryLinqTests {
    /// <summary>旧文件同毫秒历史按行号倒序，更新状态不改变排序，新记录仍使用既有九列结构。</summary>
    [Fact]
    public void ExistingHistoryKeepsInsertionOrderAndAcceptsLinqWrites() {
        using var environment = new ConfigurationTestStorage();
        var path = Path.Combine(environment.DirectoryPath, "legacy-history.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString())) {
            connection.Open();
            using var command = connection.CreateCommand();
            // 构造改用 EF Core 前的真实持久化格式，避免测试仅证明新模型能读写自身。
            command.CommandText = """
                CREATE TABLE ConfigurationChanges (
                    Id TEXT PRIMARY KEY, DocumentKey TEXT NOT NULL, PreviousRevision TEXT NOT NULL,
                    Revision TEXT NOT NULL, BeforeJson TEXT NOT NULL, AfterJson TEXT NOT NULL,
                    ChangedKeys TEXT NOT NULL, RecordedAtLocal TEXT NOT NULL, Status TEXT NOT NULL);
                PRAGMA user_version=1;
                INSERT INTO ConfigurationChanges VALUES
                    ('z-first','rules-parcel','1','2','{"value":1}','{"value":2}','["value"]','2000-01-01T00:00:00.000','Pending'),
                    ('a-second','rules-parcel','2','3','{"value":2}','{"value":3}','["value"]','2000-01-01T00:00:00.000','Pending');
                """;
            command.ExecuteNonQuery();
        }

        var history = new ConfigurationHistoryStore(path);
        Assert.Equal(new[] { "a-second", "z-first" }, history.Read().Select(row => row.Id));
        history.Complete("z-first", true);
        history.Complete("missing", false);
        var updated = history.Read();
        Assert.Equal(new[] { "a-second", "z-first" }, updated.Select(row => row.Id));
        Assert.Equal("Committed", updated[1].Status);
        Assert.Equal(new[] { "value" }, updated[1].ChangedKeys);

        var id = history.Prepare("rules-parcel", "3", "4", ConfigurationDocument.Parse("{\"value\":3}"),
            ConfigurationDocument.Parse("{\"value\":4}"));
        var reopened = new ConfigurationHistoryStore(path);
        Assert.Equal(3, reopened.Read().Length);
        Assert.Equal(id, Assert.Single(reopened.Read(0)).Id);

        using var inspection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        inspection.Open();
        using var schema = inspection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ConfigurationChanges')";
        Assert.Equal(9L, (long)schema.ExecuteScalar()!);
    }
}
