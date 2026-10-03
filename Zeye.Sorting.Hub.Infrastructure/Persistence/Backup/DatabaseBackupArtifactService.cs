using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Storage;
using Zeye.Sorting.Hub.Contracts.Models.Operations;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
/// <summary>跨平台导出 MySQL 事务快照，并仅在新数据库进行恢复及逐表核验。</summary>
public sealed class DatabaseBackupArtifactService(IConfiguration configuration, IHostEnvironment environment, IOptions<BackupOptions> options) : IDatabaseBackupArtifactService {
    /// <summary>备份及恢复串行执行，限制数据库和磁盘压力。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>清单合同采用驼峰字段及本地时间。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>备份异常及目录轮转审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>是否支持当前提供程序，SQL Server 继续使用既有原生备份 Runbook。</summary>
    public bool IsSupported => string.Equals(configuration["Persistence:Provider"], "MySql", StringComparison.OrdinalIgnoreCase);
    /// <summary>解析部署指定的持久化目录。</summary>
    private string DirectoryPath => Path.Combine(Path.IsPathRooted(options.Value.BackupDirectory) ? options.Value.BackupDirectory : Path.Combine(environment.ContentRootPath, options.Value.BackupDirectory), "MySql");
    /// <summary>读取已经完整写入的备份清单。</summary>
    public async Task<IReadOnlyList<DatabaseBackupArtifact>> ListAsync(CancellationToken ct) {
        if (!Directory.Exists(DirectoryPath)) return [];
        var artifacts = new List<DatabaseBackupArtifact>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.manifest.json").OrderByDescending(File.GetLastWriteTime).Take(200)) {
            var item = await ReadManifestAsync(path, ct);
            if (item is not null && File.Exists(ResolvePath(item.Id, ".zeye.zip"))) artifacts.Add(LocalTimes(item));
        }
        return artifacts.OrderByDescending(x => x.CreatedAtLocal).ToArray();
    }
    /// <summary>备份全库基础表与所有物理分表，使用可重复读事务保持数据一致。</summary>
    public async Task<DatabaseBackupArtifact> CreateAsync(string actor, CancellationToken ct) {
        EnsureSupported();
        if (!options.Value.IsEnabled) throw new InvalidOperationException("备份治理已禁用。");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMinutes(options.Value.OperationTimeoutMinutes));
        ct = budget.Token;
        if (!await _gate.WaitAsync(0, ct)) throw new InvalidOperationException("已有备份或恢复正在执行。");
        var id = Guid.NewGuid().ToString("N");
        string? pendingPath = null;
        string? completedPath = null;
        var published = false;
        try {
            Directory.CreateDirectory(DirectoryPath);
            pendingPath = ResolvePath(id, ".partial");
            await using var connection = new MySqlConnection(configuration.GetConnectionString("MySql"));
            await connection.OpenAsync(ct);
            // 仅备份本项目的事务表；视图、触发器或非事务表需要原生全库备份。
            var unsupported = await ScalarAsync(connection, null, "SELECT (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND (TABLE_TYPE<>'BASE TABLE' OR ENGINE<>'InnoDB')) + (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()) + (SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE()) + (SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA=DATABASE())", ct);
            if (Convert.ToInt64(unsupported, CultureInfo.InvariantCulture) != 0) throw new InvalidOperationException("数据库包含非事务表、视图或存储程序，请使用原生全库备份工具。");
            var tables = await ReadTablesAsync(connection, null, ct);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var rows = new Dictionary<string, long>();
            long exportedBytes = 0;
            await using (var output = new FileStream(pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
                foreach (var table in tables) {
                    var quoted = Quote(table);
                    using var schemaCommand = new MySqlCommand("SHOW CREATE TABLE " + quoted, connection, transaction);
                    await using var schemaReader = await schemaCommand.ExecuteReaderAsync(ct);
                    if (!await schemaReader.ReadAsync(ct)) throw new InvalidOperationException("无法读取表结构。");
                    var ddl = schemaReader.GetString(1);
                    await schemaReader.DisposeAsync();
                    var columns = new List<string>();
                    using (var columnsCommand = new MySqlCommand("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table AND EXTRA NOT LIKE '%VIRTUAL GENERATED%' AND EXTRA NOT LIKE '%STORED GENERATED%' ORDER BY ORDINAL_POSITION", connection, transaction)) {
                        columnsCommand.Parameters.AddWithValue("@table", table);
                        await using var columnsReader = await columnsCommand.ExecuteReaderAsync(ct);
                        while (await columnsReader.ReadAsync(ct)) columns.Add(columnsReader.GetString(0));
                    }
                    var entry = archive.CreateEntry(table + ".jsonl", CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                    await writer.WriteLineAsync(JsonSerializer.Serialize(ddl).AsMemory(), ct);
                    var columnList = string.Join(',', columns.Select(Quote));
                    using var command = new MySqlCommand($"SELECT {columnList} FROM {quoted}", connection, transaction) { CommandTimeout = 300 };
                    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
                    long count = 0;
                    while (await reader.ReadAsync(ct)) {
                        var values = new string[reader.FieldCount];
                        for (var i = 0; i < values.Length; i++) values[i] = SqlLiteral(reader.GetValue(i));
                        var sql = $"INSERT INTO {quoted} ({columnList}) VALUES ({string.Join(',', values)})";
                        exportedBytes += Encoding.UTF8.GetByteCount(sql);
                        if (exportedBytes > (long)options.Value.MaxExportGiB * 1024 * 1024 * 1024) throw new InvalidOperationException("备份超过配置的导出容量上限，请调整 MaxExportGiB 或使用原生备份工具。");
                        await writer.WriteLineAsync(JsonSerializer.Serialize(sql).AsMemory(), ct); count++;
                    }
                    rows[table] = count;
                }
            }
            var afterTables = await ReadTablesAsync(connection, transaction, ct);
            if (!tables.SequenceEqual(afterTables)) throw new InvalidOperationException("备份过程中表目录发生变化，请重试。");
            await transaction.CommitAsync(ct);
            var path = ResolvePath(id, ".zeye.zip"); File.Move(pendingPath, path); completedPath = path;
            var artifact = new DatabaseBackupArtifact { Id = id, Database = connection.Database, CreatedAtLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), RequestedBy = actor, TableRows = rows, SizeBytes = new FileInfo(path).Length, Sha256 = await HashAsync(path, ct) };
            await SaveAsync(artifact, ct); published = true;
            return artifact;
        }
        catch (Exception exception) { Logger.Error(exception, "数据库事务快照备份失败，Id={Id}", id); throw; }
        finally {
            // 未发布清单的文件不能被当作有效备份；清理失败不得覆盖原始故障。
            DeleteTemporaryFile(pendingPath);
            if (!published) DeleteTemporaryFile(completedPath);
            _gate.Release();
        }
    }
    /// <summary>只恢复到服务端生成的新数据库，不能接收目标库名或覆盖当前业务库。</summary>
    public async Task<DatabaseBackupArtifact> RestoreIsolatedAsync(string id, CancellationToken ct) {
        EnsureSupported();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromMinutes(options.Value.OperationTimeoutMinutes));
        ct = budget.Token;
        if (!await _gate.WaitAsync(0, ct)) throw new InvalidOperationException("已有备份或恢复正在执行。");
        try {
            var artifact = await FindVerifiedAsync(id, ct);
            var builder = new MySqlConnectionStringBuilder(configuration.GetConnectionString("MySql")) { Database = string.Empty };
            await using var connection = new MySqlConnection(builder.ConnectionString); await connection.OpenAsync(ct);
            var target = "zeye_restore_" + Guid.NewGuid().ToString("N")[..20];
            await ExecuteAsync(connection, "CREATE DATABASE " + Quote(target) + " CHARACTER SET utf8mb4", ct);
            try {
            await connection.ChangeDatabaseAsync(target, ct);
            await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS=0", ct);
            using var archive = ZipFile.OpenRead(ResolvePath(id, ".zeye.zip"));
            // 先建全体表，再插入数据，恢复所有关联表及全局定位索引。
            foreach (var table in artifact.TableRows.Keys) {
                var entry = archive.GetEntry(table + ".jsonl") ?? throw new InvalidOperationException("备份缺少表结构。");
                using var reader = new StreamReader(entry.Open());
                var line = await reader.ReadLineAsync(ct) ?? throw new InvalidOperationException("表结构为空。");
                await ExecuteAsync(connection, JsonSerializer.Deserialize<string>(line)!, ct);
            }
            await using var transaction = await connection.BeginTransactionAsync(ct);
            foreach (var table in artifact.TableRows.Keys) {
                using var reader = new StreamReader(archive.GetEntry(table + ".jsonl")!.Open());
                _ = await reader.ReadLineAsync(ct);
                while (await reader.ReadLineAsync(ct) is string line) {
                    using var command = new MySqlCommand(JsonSerializer.Deserialize<string>(line), connection, transaction) { CommandTimeout = 300 };
                    await command.ExecuteNonQueryAsync(ct);
                }
                var count = Convert.ToInt64(await ScalarAsync(connection, transaction, "SELECT COUNT(*) FROM " + Quote(table), ct), CultureInfo.InvariantCulture);
                if (count != artifact.TableRows[table]) throw new InvalidOperationException("恢复后表行数与备份清单不一致。");
            }
            await transaction.CommitAsync(ct);
            await ExecuteAsync(connection, "SET FOREIGN_KEY_CHECKS=1", ct);
            var verified = artifact with { RestoredDatabase = target, VerifiedAtLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified) };
            await SaveAsync(verified, ct); return verified;
            }
            catch (Exception exception) {
                Logger.Error(exception, "隔离恢复失败，BackupId={BackupId}, IsolatedDatabase={IsolatedDatabase}", id, target);
                // 只清理本次服务端生成的隔离库，失败时给出明确的清理目标。
                using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await ExecuteAsync(connection, "DROP DATABASE " + Quote(target), cleanupBudget.Token); }
                catch (Exception cleanupError) when (cleanupError is MySqlException or InvalidOperationException or OperationCanceledException) {
                    Logger.Error(cleanupError, "隔离恢复失败后的临时数据库清理失败，Database={Database}", target);
                    throw new InvalidOperationException("恢复失败，未完成的隔离数据库 " + target + " 无法自动清理，请检查服务器日志。", cleanupError);
                }
                throw;
            }
        }
        finally { _gate.Release(); }
    }
    /// <summary>下载前验证清单、长度和摘要，禁止路径穿越及缺失文件。</summary>
    public async Task<string> DownloadPathAsync(string id, CancellationToken ct) { await FindVerifiedAsync(id, ct); return ResolvePath(id, ".zeye.zip"); }
    /// <summary>从服务器清单读取并校验完整文件。</summary>
    private async Task<DatabaseBackupArtifact> FindVerifiedAsync(string id, CancellationToken ct) {
        var manifest = ResolvePath(id, ".manifest.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("备份不存在。");
        var artifact = await ReadManifestAsync(manifest, ct) ?? throw new InvalidOperationException("备份清单无效。");
        var file = ResolvePath(id, ".zeye.zip");
        if (!File.Exists(file) || new FileInfo(file).Length != artifact.SizeBytes || await HashAsync(file, ct) != artifact.Sha256) throw new InvalidOperationException("备份文件完整性校验失败。");
        return LocalTimes(artifact);
    }
    /// <summary>管理清单只输出本地时间字面值，不向页面附加时区偏移。</summary>
    private static DatabaseBackupArtifact LocalTimes(DatabaseBackupArtifact artifact) => artifact with { CreatedAtLocal = DateTime.SpecifyKind(artifact.CreatedAtLocal, DateTimeKind.Unspecified), VerifiedAtLocal = artifact.VerifiedAtLocal is DateTime time ? DateTime.SpecifyKind(time, DateTimeKind.Unspecified) : null };
    /// <summary>确保只访问指定备份目录中的系统生成文件。</summary>
    private string ResolvePath(string id, string extension) {
        if (id.Length != 32 || !Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("备份编号无效。");
        return Path.Combine(DirectoryPath, id + extension);
    }
    /// <summary>原子发布清单，重启后保留真实完成及核验结果。</summary>
    private async Task SaveAsync(DatabaseBackupArtifact artifact, CancellationToken ct) {
        var path = ResolvePath(artifact.Id, ".manifest.json");
        try {
            await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(artifact, JsonOptions), ct);
            File.Move(path + ".tmp", path, true);
        }
        finally { DeleteTemporaryFile(path + ".tmp"); }
    }

    /// <summary>读取单份有界清单，损坏文件被隔离并记录日志，不阻断其余有效备份。</summary>
    private async Task<DatabaseBackupArtifact?> ReadManifestAsync(string path, CancellationToken ct) {
        try {
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("备份清单超过八 MiB 上限。");
            await using var stream = File.OpenRead(path);
            var artifact = await JsonSerializer.DeserializeAsync<DatabaseBackupArtifact>(stream, JsonOptions, ct);
            if (artifact is null || Path.GetFileName(path) != artifact.Id + ".manifest.json" || !Guid.TryParseExact(artifact.Id, "N", out _) || artifact.SizeBytes <= 0 || artifact.Sha256 is not { Length: 64 })
                throw new InvalidDataException("备份清单身份、长度或摘要非法。");
            return artifact;
        }
        catch (Exception exception) when (exception is JsonException or IOException or ArgumentException or UnauthorizedAccessException) {
            Logger.Error(exception, "跳过损坏或不可读的备份清单，Path={Path}", path);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task MaintainAsync(CancellationToken cancellationToken) {
        if (!options.Value.IsEnabled || !options.Value.ArtifactRetentionEnabled || !Directory.Exists(DirectoryPath)) return;
        if (!await _gate.WaitAsync(0, cancellationToken)) return;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromMinutes(2));
        try {
            CleanupAbandonedTemporaryFiles(budget.Token);
            // 步骤 1：只接管本服务生成且长度匹配的完整文件；不删除原生备份、损坏清单或其他文件。
            var artifacts = new List<DatabaseBackupArtifact>();
            foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.manifest.json")) {
                budget.Token.ThrowIfCancellationRequested();
                var item = await ReadManifestAsync(path, budget.Token);
                if (item is not null && File.Exists(ResolvePath(item.Id, ".zeye.zip")) && new FileInfo(ResolvePath(item.Id, ".zeye.zip")).Length == item.SizeBytes)
                    artifacts.Add(item with { TableRows = [] });
                if (artifacts.Count >= 10000) throw new InvalidOperationException("完整备份超过单轮目录维护上限，请先复核历史目录。");
            }
            artifacts.Sort(static (left, right) => right.CreatedAtLocal.CompareTo(left.CreatedAtLocal));
            var retainedBytes = artifacts.Sum(static artifact => artifact.SizeBytes);
            var retainedCount = artifacts.Count;
            var cutoff = DateTime.Now.AddDays(-options.Value.ArtifactRetentionDays);
            var maxBytes = (long)options.Value.MaxRetainedGiB * 1024 * 1024 * 1024;
            var decision = ActionIsolationPolicy.Evaluate(true, !options.Value.DryRun, options.Value.DryRun, dangerousAction: true, isRollback: false);
            if (artifacts.Count <= options.Value.MinimumRetainedArtifacts
                || (artifacts.All(item => item.CreatedAtLocal >= cutoff) && retainedCount <= options.Value.MaxRetainedArtifacts && retainedBytes <= maxBytes)) return;
            // 步骤 2：先核验最低安全份数。最新文件损坏时继续保护较旧的完整文件。
            var protectedIds = new HashSet<string>(StringComparer.Ordinal);
            var invalidIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in artifacts) {
                if (await HashAsync(ResolvePath(item.Id, ".zeye.zip"), budget.Token) == item.Sha256) protectedIds.Add(item.Id);
                else { invalidIds.Add(item.Id); Logger.Error("备份轮转发现损坏文件，保留并隔离。Id={Id}", item.Id); }
                if (protectedIds.Count >= options.Value.MinimumRetainedArtifacts) break;
            }
            if (protectedIds.Count < options.Value.MinimumRetainedArtifacts) {
                Logger.Warn("可核验备份不足最低安全份数，跳过目录轮转。VerifiedCount={VerifiedCount}", protectedIds.Count);
                return;
            }
            // 步骤 3：从最旧完整备份轮转，不删除安全份数及已知损坏文件。
            for (var index = artifacts.Count - 1; index >= 0; index--) {
                budget.Token.ThrowIfCancellationRequested();
                var item = artifacts[index];
                if (protectedIds.Contains(item.Id) || invalidIds.Contains(item.Id)) continue;
                if (item.CreatedAtLocal >= cutoff && retainedCount <= options.Value.MaxRetainedArtifacts && retainedBytes <= maxBytes) continue;
                Logger.Info("备份轮转审计：Id={Id}, CreatedAtLocal={CreatedAtLocal}, SizeBytes={SizeBytes}, Decision={Decision}", item.Id, item.CreatedAtLocal, item.SizeBytes, decision);
                if (decision == Domain.Enums.ActionIsolationDecision.Execute) {
                    // 删除前验证摘要，无法确认身份的文件始终保留。
                    if (await HashAsync(ResolvePath(item.Id, ".zeye.zip"), budget.Token) != item.Sha256) {
                        Logger.Error("备份轮转摘要不匹配，保留文件。Id={Id}", item.Id);
                        continue;
                    }
                    File.Delete(ResolvePath(item.Id, ".zeye.zip"));
                    File.Delete(ResolvePath(item.Id, ".manifest.json"));
                }
                retainedCount--; retainedBytes -= item.SizeBytes;
            }
            if (retainedBytes > maxBytes) Logger.Warn("最低安全备份占用已超过配置容量，保留有效备份并提示扩容。SizeBytes={SizeBytes}, MaxBytes={MaxBytes}", retainedBytes, maxBytes);
        }
        catch (Exception exception) { Logger.Error(exception, "数据库备份目录维护失败。"); throw; }
        finally { _gate.Release(); }
    }

    /// <summary>轮转超过两天的服务生成临时文件，遵循预演开关并保留其他目录内容。</summary>
    private void CleanupAbandonedTemporaryFiles(CancellationToken cancellationToken) {
        var cutoff = DateTime.Now.AddDays(-2);
        foreach (var extension in new[] { ".partial", ".manifest.json.tmp" }) {
            foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*" + extension)) {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(path);
                if (!Guid.TryParseExact(name[..^extension.Length], "N", out _) || File.GetLastWriteTime(path) >= cutoff) continue;
                Logger.Warn("清理中断备份临时文件：Path={Path}, DryRun={DryRun}", path, options.Value.DryRun);
                if (!options.Value.DryRun) File.Delete(path);
            }
        }
    }

    /// <summary>清理当前操作产生的临时文件，保留原始异常且确保并发锁总能释放。</summary>
    private static void DeleteTemporaryFile(string? path) {
        if (path is null) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Logger.Error(exception, "备份临时文件清理失败，Path={Path}", path); }
    }
    /// <summary>获取完整文件的校验摘要。</summary>
    private static async Task<string> HashAsync(string path, CancellationToken ct) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)); }
    /// <summary>提供程序不匹配时明确拒绝。</summary>
    private void EnsureSupported() { if (!IsSupported) throw new InvalidOperationException("页面备份暂支持 MySQL；当前提供程序请使用原生备份 Runbook。"); }
    /// <summary>按 MySQL 标识符规则转义来自系统目录的表列名称。</summary>
    private static string Quote(string name) => "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
    /// <summary>使用十六进制保存文本及二进制数据，避免 SQL 字符串转义和数字精度损失。</summary>
    internal static string SqlLiteral(object value) => value switch {
        DBNull => "NULL", byte[] bytes => "X'" + Convert.ToHexString(bytes) + "'",
        string text => "CONVERT(X'" + Convert.ToHexString(Encoding.UTF8.GetBytes(text)) + "' USING utf8mb4)",
        DateTime date => "'" + date.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "'",
        TimeSpan span => "'" + (span < TimeSpan.Zero ? "-" : "") + (Math.Abs(span.Ticks) / TimeSpan.TicksPerHour).ToString(CultureInfo.InvariantCulture) + span.Duration().ToString(@"\:mm\:ss\.ffffff", CultureInfo.InvariantCulture) + "'",
        bool flag => flag ? "1" : "0",
        sbyte or byte or short or ushort or int or uint or long or ulong or decimal => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        _ => throw new InvalidOperationException("备份遇到不支持的字段类型。")
    };
    /// <summary>读取当前数据库的基础表目录。</summary>
    private static async Task<List<string>> ReadTablesAsync(MySqlConnection connection, MySqlTransaction? transaction, CancellationToken ct) {
        using var command = new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct); var tables = new List<string>();
        while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0)); return tables;
    }
    /// <summary>执行标量查询，复用事务及连接。</summary>
    private static async Task<object?> ScalarAsync(MySqlConnection connection, MySqlTransaction? transaction, string sql, CancellationToken ct) { using var command = new MySqlCommand(sql, connection, transaction); return await command.ExecuteScalarAsync(ct); }
    /// <summary>执行只作用于新数据库的结构命令。</summary>
    private static async Task ExecuteAsync(MySqlConnection connection, string sql, CancellationToken ct) { using var command = new MySqlCommand(sql, connection) { CommandTimeout = 300 }; await command.ExecuteNonQueryAsync(ct); }
}
