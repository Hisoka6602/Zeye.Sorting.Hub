using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>启动时将旧清理清单转换为批次汇总，保留原操作信息及提交凭据。</summary>
public static class ParcelCleanupAuditCompactor {
    /// <summary>回滚文件目录，可填写可写的绝对或相对目录，默认治理制品下的 cleanup-audit-rollback。</summary>
    public const string RollbackDirectoryConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:AuditCompaction:RollbackDirectory";
    /// <summary>每次读取的操作键数量，避免加载全部历史。</summary>
    private const int OperationPageSize = 20;
    /// <summary>历史单次删除上限，超过上限的异常记录不自动转换。</summary>
    private const int MaximumBatchCount = 10000;
    /// <summary>与永久记录相同的序列化合同。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>仅记录操作编号与汇总数量，不输出包裹快照或凭据。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>按操作逐个事务转换；损坏、执行中或隔离器阻断的记录保持原样。</summary>
    public static async Task<int> CompactAsync(SortingHubDbContext db, IConfiguration configuration, CancellationToken ct) {
        if (!db.Database.IsRelational()) return 0;
        var decision = ParcelCleanupIsolationPolicy.Evaluate(configuration);
        var configuredDirectory = configuration[RollbackDirectoryConfigKey];
        var directory = string.IsNullOrWhiteSpace(configuredDirectory) ? Path.Combine("governance-artifacts", "cleanup-audit-rollback") : configuredDirectory;
        var cursor = string.Empty;
        var converted = 0;
        while (true) {
            var keys = await db.Set<ManagedDocument>().AsNoTracking().Where(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix) && x.Key.CompareTo(cursor) > 0)
                .OrderBy(x => x.Key).Select(x => x.Key).Take(OperationPageSize).ToListAsync(ct);
            if (keys.Count == 0) break;
            cursor = keys[^1];
            foreach (var key in keys) {
                try {
                    // 每个操作独立提交，损坏记录不会阻塞其他历史，也不能留下半份转换。
                    if (await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                        db.ChangeTracker.Clear();
                        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
                        var header = await db.Set<ManagedDocument>().AsTracking().SingleAsync(x => x.Key == key, ct);
                        var audit = JsonSerializer.Deserialize<ParcelCleanupAudit>(header.Json, JsonOptions) ?? throw new InvalidOperationException("清理操作汇总为空。");
                        if (audit.StorageFormat == ParcelCleanupAudit.SummaryStorageFormat || audit.Status == "running") return false;
                        if (!Guid.TryParseExact(audit.Id, "N", out _) || key != ParcelCleanupAudit.Prefix + audit.Id || audit.BatchCount is < 0 or > MaximumBatchCount)
                            throw new InvalidOperationException("清理操作编号或批次数量无效。");
                        var prefix = ParcelCleanupAudit.BatchPrefix(audit.Id);
                        var batches = await db.Set<ManagedDocument>().AsTracking().Where(x => x.Key.StartsWith(prefix)).OrderBy(x => x.Key).Take(MaximumBatchCount + 1).ToListAsync(ct);
                        if (batches.Count != audit.BatchCount) throw new InvalidOperationException("清理批次数与操作汇总不一致。");
                        var summaries = batches.Select(ReadBatchSummary).ToArray();
                        if (summaries.Sum(x => x.DeletedCount) != audit.ExecutedCount) throw new InvalidOperationException("历史清单数量与实际删除数量不一致。");
                        if (decision != ActionIsolationDecision.Execute) {
                            Logger.Info("清理历史精简审计，Decision={Decision}, CleanupRecordId={CleanupRecordId}, ExecutedCount={ExecutedCount}", decision, audit.Id, audit.ExecutedCount);
                            return false;
                        }
                        // 先刷盘保存带并发条件的压缩回滚脚本，失败时绝不覆盖旧载荷。
                        var originals = batches.Prepend(header).ToArray();
                        var bytesBefore = originals.Sum(x => Encoding.UTF8.GetByteCount(x.Json));
                        var rollbackFile = await WriteRollbackAsync(db, originals, directory, audit.Id, ct);
                        for (var i = 0; i < batches.Count; i++) {
                            batches[i].Json = JsonSerializer.Serialize(summaries[i], JsonOptions);
                            batches[i].Revision++;
                        }
                        header.Json = JsonSerializer.Serialize(audit with { StorageFormat = ParcelCleanupAudit.SummaryStorageFormat,
                            Scope = ParcelCleanupAudit.SummaryScope, CompensationBoundary = ParcelCleanupAudit.SummaryCompensationBoundary }, JsonOptions);
                        header.Revision++;
                        await db.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);
                        Logger.Info("清理历史精简完成，CleanupRecordId={CleanupRecordId}, ExecutedCount={ExecutedCount}, BytesBefore={BytesBefore}, BytesAfter={BytesAfter}, RollbackFile={RollbackFile}",
                            audit.Id, audit.ExecutedCount, bytesBefore, originals.Sum(x => Encoding.UTF8.GetByteCount(x.Json)), rollbackFile);
                        return true;
                    })) converted++;
                } catch (OperationCanceledException ex) {
                    Logger.Warn(ex, "清理历史精简已取消，RecordKey={RecordKey}", key);
                    throw;
                } catch (Exception ex) {
                    Logger.Error(ex, "清理历史精简失败，旧操作及清单保留，RecordKey={RecordKey}", key);
                }
            }
        }
        db.ChangeTracker.Clear();
        return converted;
    }

    /// <summary>旧数组只提取数量；新格式保留已有批次汇总，不反序列化逐票业务字段。</summary>
    private static ParcelCleanupBatchAudit ReadBatchSummary(ManagedDocument document) {
        using var json = JsonDocument.Parse(document.Json);
        if (json.RootElement.ValueKind == JsonValueKind.Array)
            return new ParcelCleanupBatchAudit { DeletedCount = json.RootElement.GetArrayLength(), CommittedAtLocal = document.ModifiedAt };
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("deletedCount", out var count) || !count.TryGetInt32(out var deletedCount))
            throw new InvalidOperationException("清理批次格式无效。");
        var summary = JsonSerializer.Deserialize<ParcelCleanupBatchAudit>(document.Json, JsonOptions) ?? throw new InvalidOperationException("清理批次汇总为空。");
        if (deletedCount is < 0 or > MaximumBatchCount) throw new InvalidOperationException("清理批次删除数量无效。");
        return summary;
    }

    /// <summary>通过 EF 方言生成 SQL 字面量，压缩保存原载荷；回滚不覆盖已再次修改的记录。</summary>
    private static async Task<string> WriteRollbackAsync(SortingHubDbContext db, IReadOnlyList<ManagedDocument> documents, string directory, string id, CancellationToken ct) {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, id + "-" + Guid.NewGuid().ToString("N") + ".rollback.sql.gz");
        var sql = db.GetService<ISqlGenerationHelper>();
        var mapping = db.GetService<IRelationalTypeMappingSource>();
        var strings = mapping.FindMapping(typeof(string))!;
        var dates = mapping.FindMapping(typeof(DateTime))!;
        var entity = db.Model.FindEntityType(typeof(ManagedDocument))!;
        var table = sql.DelimitIdentifier(entity.GetTableName()!, entity.GetSchema());
        var key = sql.DelimitIdentifier(nameof(ManagedDocument.Key));
        var json = sql.DelimitIdentifier(nameof(ManagedDocument.Json));
        var revision = sql.DelimitIdentifier(nameof(ManagedDocument.Revision));
        var modifiedAt = sql.DelimitIdentifier(nameof(ManagedDocument.ModifiedAt));
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize, leaveOpen: true)) {
            await using var writer = new StreamWriter(gzip, new UTF8Encoding(false), leaveOpen: true);
            await writer.WriteLineAsync("-- 清理历史升级回滚：停用历史精简或切换为演练后解压执行；并发条件不覆盖已再次修改的记录。");
            await writer.WriteLineAsync(db.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true ? "START TRANSACTION;" : "BEGIN TRANSACTION;");
            foreach (var document in documents) {
                ct.ThrowIfCancellationRequested();
                await writer.WriteLineAsync($"UPDATE {table} SET {json}={strings.GenerateSqlLiteral(document.Json)}, {revision}={revision}+1, {modifiedAt}={dates.GenerateSqlLiteral(document.ModifiedAt)} WHERE {key}={strings.GenerateSqlLiteral(document.Key)} AND {revision}={document.Revision + 1};");
            }
            await writer.WriteLineAsync("COMMIT;");
            await writer.FlushAsync(ct);
        }
        file.Flush(flushToDisk: true);
        return path;
    }
}
