using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>物理建表会话锁和结构探测，防止多实例竞争及部分DDL失败后无法重试。</summary>
internal sealed class ParcelPartitionDdlCoordinator : IAsyncDisposable {
    /// <summary>保持锁和DDL使用同一数据库会话。</summary>
    private readonly SortingHubDbContext _db;
    /// <summary>当前提供器名称。</summary>
    private readonly string _provider;
    /// <summary>周期锁资源名，长度小于MySQL的64字符限制。</summary>
    private readonly string _resource;
    /// <summary>锁释放失败的审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>建立会话协调器。</summary>
    private ParcelPartitionDdlCoordinator(SortingHubDbContext db, string suffix) {
        _db = db; _provider = db.Database.ProviderName ?? string.Empty; _resource = "SortingHub.Parcel.Partition." + suffix;
    }

    /// <summary>获取生产数据库独占会话锁；SQLite测试由进程内有界锁保护。</summary>
    public static async Task<ParcelPartitionDdlCoordinator> AcquireAsync(SortingHubDbContext db, string suffix, CancellationToken token) {
        await db.Database.OpenConnectionAsync(token);
        var coordinator = new ParcelPartitionDdlCoordinator(db, suffix);
        if (coordinator._provider.Contains("MySql", StringComparison.OrdinalIgnoreCase)) {
            var result = await coordinator.ScalarAsync("SELECT GET_LOCK(@resource, 30)", token, ("@resource", coordinator._resource));
            if (Convert.ToInt32(result) != 1) throw new InvalidOperationException("获取包裹分表建表锁超时。");
        }
        else if (coordinator._provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)) {
            var result = await coordinator.ScalarAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=30000; SELECT @result;", token, ("@resource", coordinator._resource));
            if (Convert.ToInt32(result) < 0) throw new InvalidOperationException("获取包裹分表建表锁失败。");
        }
        return coordinator;
    }

    /// <summary>读取真实表列名；不存在返回空集合。</summary>
    public async Task<HashSet<string>> ReadColumnsAsync(string table, string? schema, CancellationToken token) {
        var sql = _provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "SELECT name FROM pragma_table_info(@table)"
            : _provider.Contains("MySql", StringComparison.OrdinalIgnoreCase)
                ? "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table"
                : "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@schema AND TABLE_NAME=@table";
        await using var command = CreateCommand(sql, ("@table", table), ("@schema", schema ?? "dbo"));
        await using var reader = await command.ExecuteReaderAsync(token);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(token)) columns.Add(reader.GetString(0));
        return columns;
    }

    /// <summary>探测已成功建立的索引，重试时跳过已有索引。</summary>
    public async Task<bool> IndexExistsAsync(string table, string? schema, string index, CancellationToken token) {
        var sql = _provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND tbl_name=@table AND name=@index"
            : _provider.Contains("MySql", StringComparison.OrdinalIgnoreCase)
                ? "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table AND INDEX_NAME=@index"
                : "SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE s.name=@schema AND t.name=@table AND i.name=@index";
        return Convert.ToInt32(await ScalarAsync(sql, token, ("@table", table), ("@schema", schema ?? "dbo"), ("@index", index))) > 0;
    }

    /// <summary>创建参数化命令，命令文本仅来自内部常量。</summary>
    private DbCommand CreateCommand(string sql, params (string Name, string Value)[] parameters) {
        var command = _db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 40;
        foreach (var value in parameters) {
            var parameter = command.CreateParameter(); parameter.ParameterName = value.Name; parameter.Value = value.Value; command.Parameters.Add(parameter);
        }
        return command;
    }

    /// <summary>执行结构探测或锁命令。</summary>
    private async Task<object?> ScalarAsync(string sql, CancellationToken token, params (string Name, string Value)[] parameters) {
        await using var command = CreateCommand(sql, parameters);
        return await command.ExecuteScalarAsync(token);
    }

    /// <summary>释放会话锁；失败时记录日志且关闭连接，不覆盖原始DDL错误。</summary>
    public async ValueTask DisposeAsync() {
        try {
            if (_provider.Contains("MySql", StringComparison.OrdinalIgnoreCase)) await ScalarAsync("SELECT RELEASE_LOCK(@resource)", default, ("@resource", _resource));
            else if (_provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)) await ScalarAsync("EXEC sys.sp_releaseapplock @Resource=@resource, @LockOwner='Session';", default, ("@resource", _resource));
        }
        catch (Exception exception) { Logger.Error(exception, "包裹分表会话锁释放失败，Resource={Resource}", _resource); }
        finally { await _db.Database.CloseConnectionAsync(); }
    }
}
