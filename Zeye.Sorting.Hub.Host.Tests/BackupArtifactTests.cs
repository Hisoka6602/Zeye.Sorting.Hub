using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;
using Zeye.Sorting.Hub.Contracts.Models.Operations;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
namespace Zeye.Sorting.Hub.Host.Tests;
/// <summary>备份目录失败后的并发锁释放回归，避免所有后续备份永久阻塞。</summary>
public sealed class BackupArtifactTests {
    /// <summary>真实快照校验必须核对摘要，等长损坏不能继续作为健康备份。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotVerificationChecksDigestBeforeReportingSuccess(bool corrupt) {
        var root = Path.Combine(Path.GetTempPath(), "zeye-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MySql"));
        try {
            await WriteArtifactAsync(root, 0, corrupt);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql", ["ConnectionStrings:MySql"] = "Server=localhost;Database=test;User ID=test;Password=<test-password>;"
            }).Build();
            var environment = new HostingEnvironment { ContentRootPath = root };
            var options = Microsoft.Extensions.Options.Options.Create(new BackupOptions { BackupDirectory = root });
            var artifacts = new DatabaseBackupArtifactService(configuration, environment, options);
            var verification = new BackupVerificationService(new MySqlBackupProvider(), options, configuration, new RestoreDrillPlanner(environment), artifacts);
            var record = await verification.ExecuteAsync(default);
            Assert.Equal(corrupt ? BackupExecutionRecord.FailedStatus : BackupExecutionRecord.CompletedStatus, record.Status);
            Assert.Equal(!corrupt, record.HasBackupFile);
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>轮转只删除已确认的旧备份，并始终保留最新最低安全份数。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArtifactRotationHonorsDryRunAndMinimumVerifiedCopies(bool dryRun) {
        var root = Path.Combine(Path.GetTempPath(), "zeye-rotation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MySql"));
        try {
            var latest = await WriteArtifactAsync(root, 0);
            var second = await WriteArtifactAsync(root, 1);
            var old = await WriteArtifactAsync(root, 60);
            var foreign = Path.Combine(root, "MySql", "native-backup.sql");
            await File.WriteAllTextAsync(foreign, "保留原生文件");
            var partial = Path.Combine(root, "MySql", Guid.NewGuid().ToString("N") + ".partial");
            await File.WriteAllTextAsync(partial, "中断导出");
            File.SetLastWriteTime(partial, DateTime.Now.AddDays(-3));
            var service = CreateService(root, dryRun);
            await service.MaintainAsync(default);
            Assert.True(File.Exists(ArtifactPath(root, latest.Id)));
            Assert.True(File.Exists(ArtifactPath(root, second.Id)));
            Assert.Equal(dryRun, File.Exists(ArtifactPath(root, old.Id)));
            Assert.Equal(dryRun, File.Exists(partial));
            Assert.True(File.Exists(foreign));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>最新文件损坏时，保留较旧但完整的安全副本，避免删掉唯一恢复来源。</summary>
    [Fact]
    public async Task CorruptNewestFileCannotReplaceProtectedVerifiedBackups() {
        var root = Path.Combine(Path.GetTempPath(), "zeye-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MySql"));
        try {
            var damaged = await WriteArtifactAsync(root, 0, corrupt: true);
            var good = await WriteArtifactAsync(root, 1);
            var goodOld = await WriteArtifactAsync(root, 60);
            var redundantOld = await WriteArtifactAsync(root, 61);
            await CreateService(root, false).MaintainAsync(default);
            Assert.True(File.Exists(ArtifactPath(root, damaged.Id)));
            Assert.True(File.Exists(ArtifactPath(root, good.Id)));
            Assert.True(File.Exists(ArtifactPath(root, goodOld.Id)));
            Assert.False(File.Exists(ArtifactPath(root, redundantOld.Id)));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>不足两份可核验备份时不轮转，即使文件数量或年龄超过配置。</summary>
    [Fact]
    public async Task InsufficientVerifiedCopiesPreventsDeletion() {
        var root = Path.Combine(Path.GetTempPath(), "zeye-last-copy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MySql"));
        try {
            await WriteArtifactAsync(root, 0, true);
            await WriteArtifactAsync(root, 1, true);
            var lastGood = await WriteArtifactAsync(root, 60);
            await CreateService(root, false).MaintainAsync(default);
            Assert.True(File.Exists(ArtifactPath(root, lastGood.Id)));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>损坏清单不阻断其余有效备份的列举。</summary>
    [Fact]
    public async Task InvalidManifestDoesNotHideValidArtifacts() {
        var root = Path.Combine(Path.GetTempPath(), "zeye-manifests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MySql"));
        try {
            var good = await WriteArtifactAsync(root, 0);
            await File.WriteAllTextAsync(Path.Combine(root, "MySql", Guid.NewGuid().ToString("N") + ".manifest.json"), "{broken");
            var listed = await CreateService(root, true).ListAsync(default);
            Assert.Equal(good.Id, Assert.Single(listed).Id);
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>构建只依赖本地目录的备份治理实例，不访问业务数据库。</summary>
    private static DatabaseBackupArtifactService CreateService(string root, bool dryRun) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Persistence:Provider"] = "MySql" }).Build();
        return new DatabaseBackupArtifactService(configuration, new HostingEnvironment { ContentRootPath = root }, Microsoft.Extensions.Options.Options.Create(
            new BackupOptions { BackupDirectory = root, DryRun = dryRun, MinimumRetainedArtifacts = 2, MaxRetainedArtifacts = 2 }));
    }

    /// <summary>写入真实摘要和长度清单，可选择注入等长损坏。</summary>
    private static async Task<DatabaseBackupArtifact> WriteArtifactAsync(string root, int ageDays, bool corrupt = false) {
        var id = Guid.NewGuid().ToString("N");
        var bytes = System.Text.Encoding.UTF8.GetBytes("备份内容-" + id);
        var artifact = new DatabaseBackupArtifact {
            Id = id, Database = "test", RequestedBy = "测试", CreatedAtLocal = DateTime.Now.AddDays(-ageDays),
            SizeBytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), TableRows = []
        };
        if (corrupt) bytes[0] ^= 1;
        await File.WriteAllBytesAsync(ArtifactPath(root, id), bytes);
        await File.WriteAllTextAsync(Path.Combine(root, "MySql", id + ".manifest.json"), JsonSerializer.Serialize(artifact, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return artifact;
    }

    /// <summary>返回本测试目录中服务生成的备份路径。</summary>
    private static string ArtifactPath(string root, string id) => Path.Combine(root, "MySql", id + ".zeye.zip");

    /// <summary>目录路径实际为文件时，两次请求均报告目录错误而非虚假任务占用。</summary>
    [Fact]
    public async Task FailedDirectoryCreationReleasesBackupGate() {
        var path = Path.Combine(Path.GetTempPath(), "zeye-backup-gate-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(path, "blocked directory");
        try {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Persistence:Provider"] = "MySql" }).Build();
            var service = new DatabaseBackupArtifactService(configuration, new HostingEnvironment { ContentRootPath = Path.GetTempPath() }, Microsoft.Extensions.Options.Options.Create(new BackupOptions { BackupDirectory = path }));
            await Assert.ThrowsAnyAsync<IOException>(() => service.CreateAsync("test", default));
            await Assert.ThrowsAnyAsync<IOException>(() => service.CreateAsync("test", default));
        }
        finally { File.Delete(path); }
    }
}
