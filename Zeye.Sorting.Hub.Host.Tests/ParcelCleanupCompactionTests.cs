using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>历史清单升级保留准确操作信息，并验证隔离器、事务失败与可执行回滚脚本。</summary>
public sealed class ParcelCleanupCompactionTests : IDisposable {
    /// <summary>本测试独占的回滚制品目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "zeye-cleanup-compaction-" + Guid.NewGuid().ToString("N"));
    /// <summary>永久操作 JSON 的序列化格式。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>旧清单转为少量汇总，二次执行无写入，回滚文件可完整恢复原审计载荷。</summary>
    [Fact]
    public async Task LegacyCompactionPreservesOperationsAndCanBeRolledBack() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var id = await SeedLegacyAsync(db);
        await using var context = await db.Factory.CreateDbContextAsync();
        var before = await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).ToArrayAsync();
        Assert.Equal(1, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(), default));
        var after = await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).ToArrayAsync();
        var original = JsonSerializer.Deserialize<ParcelCleanupAudit>(before.Single(x => x.Key == ParcelCleanupAudit.Prefix + id).Json, JsonOptions)!;
        var updated = JsonSerializer.Deserialize<ParcelCleanupAudit>(after.Single(x => x.Key == ParcelCleanupAudit.Prefix + id).Json, JsonOptions)!;
        Assert.Equal(original with { StorageFormat = ParcelCleanupAudit.SummaryStorageFormat, Scope = ParcelCleanupAudit.SummaryScope,
            CompensationBoundary = ParcelCleanupAudit.SummaryCompensationBoundary }, updated);
        Assert.Equal(3, after.Where(x => x.Key.StartsWith(ParcelCleanupAudit.BatchPrefix(id), StringComparison.Ordinal)).Sum(x => JsonSerializer.Deserialize<ParcelCleanupBatchAudit>(x.Json, JsonOptions)!.DeletedCount));
        Assert.All(after, x => { Assert.DoesNotContain("PRIVATE-PARCEL", x.Json); Assert.DoesNotContain("barCodes", x.Json); });
        Assert.Equal(0, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(), default));
        Assert.Single(Directory.GetFiles(_directory));
        Assert.Equal(after.Select(x => x.Revision), (await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).ToArrayAsync()).Select(x => x.Revision));
        using var gzip = new GZipStream(File.OpenRead(Directory.GetFiles(_directory).Single()), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = await reader.ReadToEndAsync();
        await command.ExecuteNonQueryAsync();
        Assert.Equal(before.Select(x => x.Json), (await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).ToArrayAsync()).Select(x => x.Json));
    }

    /// <summary>演练与阻断不修改历史，也不生成包含身份快照的回滚文件。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task IsolationDoesNotMutateLegacyRecords(bool allowed, bool dryRun) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync(); await SeedLegacyAsync(db);
        await using var context = await db.Factory.CreateDbContextAsync();
        var before = await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync();
        Assert.Equal(0, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(allowed, dryRun), default));
        Assert.Equal(before, await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync());
        Assert.False(Directory.Exists(_directory));
    }

    /// <summary>无法保存回滚文件时保持旧清单与原汇总，不能先删除审计明细。</summary>
    [Fact]
    public async Task UnwritableRollbackDirectoryKeepsLegacyData() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync(); await SeedLegacyAsync(db);
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "occupied"); await File.WriteAllTextAsync(file, "测试文件，不能作为目录");
        await using var context = await db.Factory.CreateDbContextAsync();
        var before = await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync();
        var settings = Configuration(); settings[ParcelCleanupAuditCompactor.RollbackDirectoryConfigKey] = file;
        Assert.Equal(0, await ParcelCleanupAuditCompactor.CompactAsync(context, settings, default));
        Assert.Equal(before, await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync());
    }

    /// <summary>SQL更新后提交前失败时，旧清单和汇总一起回滚，下一次启动可以继续转换。</summary>
    [Fact]
    public async Task CompactionFailureRollsBackAllDocumentsAndCanRetry() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync(); await SeedLegacyAsync(db);
        await using var context = await db.Factory.CreateDbContextAsync();
        var before = await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync();
        db.Failure.FailCleanupBatchNumber = 1;
        Assert.Equal(0, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(), default));
        Assert.Equal(before, await context.Set<ManagedDocument>().AsNoTracking().OrderBy(x => x.Key).Select(x => x.Json).ToArrayAsync());
        Assert.Equal(1, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(), default));
    }

    /// <summary>执行中的操作与数量不一致的历史均保持原样，其他完整操作仍正常转换。</summary>
    [Fact]
    public async Task RunningOrInconsistentHistoryDoesNotBlockValidOperation() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var running = await SeedLegacyAsync(db, "running"); var invalid = await SeedLegacyAsync(db, executedCount: 4); var valid = await SeedLegacyAsync(db);
        await using var context = await db.Factory.CreateDbContextAsync();
        Assert.Equal(1, await ParcelCleanupAuditCompactor.CompactAsync(context, Configuration(), default));
        var headers = await context.Set<ManagedDocument>().AsNoTracking().Where(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix)).ToArrayAsync();
        Assert.Null(JsonSerializer.Deserialize<ParcelCleanupAudit>(headers.Single(x => x.Key == ParcelCleanupAudit.Prefix + running).Json, JsonOptions)!.StorageFormat);
        Assert.Null(JsonSerializer.Deserialize<ParcelCleanupAudit>(headers.Single(x => x.Key == ParcelCleanupAudit.Prefix + invalid).Json, JsonOptions)!.StorageFormat);
        Assert.Equal(ParcelCleanupAudit.SummaryStorageFormat, JsonSerializer.Deserialize<ParcelCleanupAudit>(headers.Single(x => x.Key == ParcelCleanupAudit.Prefix + valid).Json, JsonOptions)!.StorageFormat);
    }

    /// <summary>固定隔离配置及测试独占的回滚目录。</summary>
    private IConfigurationRoot Configuration(bool allowed = true, bool dryRun = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        [ParcelCleanupIsolationPolicy.EnableGuardConfigKey] = "true", [ParcelCleanupIsolationPolicy.AllowExecutionConfigKey] = allowed.ToString(),
        [ParcelCleanupIsolationPolicy.DryRunConfigKey] = dryRun.ToString(), [ParcelCleanupAuditCompactor.RollbackDirectoryConfigKey] = _directory
    }).Build();

    /// <summary>构造含引号、反斜杠及中文的旧版逐票快照，验证回滚字面量完整性。</summary>
    private static async Task<string> SeedLegacyAsync(RelationalParcelTestDatabase db, string status = "completed", int executedCount = 3) {
        var audit = new ParcelCleanupAudit { Id = Guid.NewGuid().ToString("N"), Operator = new("user", "operator", "历史操作人", "127.0.0.1", "trace"),
            CreatedBefore = new(2026, 10, 1), StartedAtLocal = new(2026, 10, 2, 12, 0, 0), CompletedAtLocal = new(2026, 10, 2, 12, 1, 0),
            Decision = "execute", Status = status, PlannedCount = 3, ExecutedCount = executedCount, BatchCount = 2, Scope = "旧清理范围", CompensationBoundary = "旧清单永久保留" };
        await using var context = await db.Factory.CreateDbContextAsync();
        context.Add(new ManagedDocument { Key = ParcelCleanupAudit.Prefix + audit.Id, Json = JsonSerializer.Serialize(audit, JsonOptions), Revision = 2, ModifiedAt = audit.StartedAtLocal });
        for (var i = 1; i <= 2; i++) context.Add(new ManagedDocument { Key = ParcelCleanupAudit.BatchPrefix(audit.Id) + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Revision = 1, ModifiedAt = audit.StartedAtLocal.AddSeconds(i),
            Json = JsonSerializer.Serialize(Enumerable.Range(1, i).Select(x => new { id = i * 100 + x, barCodes = "PRIVATE-PARCEL-'\\-" + x, workstationName = "历史工作台" }).ToArray(), JsonOptions) });
        await context.SaveChangesAsync();
        return audit.Id;
    }

    /// <summary>只删除本测试在临时目录创建的独占制品目录。</summary>
    public void Dispose() {
        var path = Path.GetFullPath(_directory);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("zeye-cleanup-compaction-", StringComparison.Ordinal))
            throw new InvalidOperationException("测试制品目录超出清理范围。");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
