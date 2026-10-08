using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证现有管理接口迁移到 LiteDB 后仍保持业务归属与安全约束。</summary>
public sealed class LiteDbManagedConfigurationTests {
    /// <summary>旧 JSON 中的机器密钥在目录加密导入完成后不再存在于运行配置种子中。</summary>
    [Fact]
    public async Task LegacyFusionCredentialSeedsAreRemovedAfterEncryptedImport() {
        using var storage = new ConfigurationTestStorage();
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var runtime = storage.Store.ReadRuntime();
        runtime["FusionIngestion"]!["Sources"] = JsonSerializer.SerializeToNode(env.Options.Sources);
        Assert.True(storage.Store.WriteRuntime(storage.Source.Capture().Revision, runtime));
        var service = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), new EphemeralDataProtectionProvider(), storage.Store);
        await service.InitializeAsync(default);
        Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, storage.Store.ReadRuntime().ToJsonString());
        Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, storage.Store.Read("fusion-ingestion-directory")!.Json);
        Assert.Contains(storage.History.Read(), entry => entry.DocumentKey == "runtime"
            && entry.BeforeJson.Contains(FusionIngressTestEnvironment.FirstKey, StringComparison.Ordinal)
            && !entry.AfterJson.Contains(FusionIngressTestEnvironment.FirstKey, StringComparison.Ordinal));
        Assert.Equal(2, service.Snapshot.Sources.Count);
    }

    /// <summary>旧加密目录导入保持版本和密钥，轮换只更新 LiteDB 并立即撤销旧连接。</summary>
    [Fact]
    public async Task EncryptedFusionDirectoryMigratesAndRevokesOldLeasesOnRotation() {
        using var storage = new ConfigurationTestStorage();
        await using var env = new FusionIngressTestEnvironment(); await env.InitializeAsync();
        var protection = new EphemeralDataProtectionProvider();
        var legacy = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection);
        await legacy.InitializeAsync(default);
        var oldRevision = legacy.Snapshot.Revision;
        await new LegacyConfigurationMigrationHostedService(env.Database.Factory, storage.Store).StartAsync(default);
        var migrated = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection, storage.Store);
        await migrated.InitializeAsync(default);
        Assert.Equal(oldRevision, migrated.Snapshot.Revision);
        var ingress = new FusionIngestionService(env.Database.Factory, MsOptions.Create(env.Options), env.Root, migrated);
        await ingress.RegisterAsync("connection", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default);
        var rotated = await migrated.RotateKeyAsync(oldRevision, "fusion-line-01", default);
        Assert.NotNull(rotated);
        Assert.False(ingress.Authenticate("fusion-line-01", FusionIngressTestEnvironment.FirstKey));
        Assert.True(ingress.Authenticate("fusion-line-01", rotated.Pairing.MachineApiKey));
        Assert.True(ingress.Authenticate("fusion-line-02", FusionIngressTestEnvironment.SecondKey));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ingress.RegisterAsync("connection", "fusion-line-01", FusionIngressTestEnvironment.Hello(), default));
        Assert.Null(await migrated.RotateKeyAsync(oldRevision, "fusion-line-01", default));
        var encrypted = storage.Store.Read("fusion-ingestion-directory")!;
        Assert.DoesNotContain(rotated.Pairing.MachineApiKey, encrypted.Json);
        Assert.DoesNotContain(FusionIngressTestEnvironment.FirstKey, encrypted.Json);
        Assert.DoesNotContain(rotated.Pairing.MachineApiKey, JsonSerializer.Serialize(storage.History.Read()));
        var restarted = new FusionConfigurationService(env.Database.Factory, MsOptions.Create(env.Options), protection, storage.Store);
        await restarted.InitializeAsync(default);
        Assert.Equal(rotated.Revision, restarted.Snapshot.Revision);
        await using var db = await env.Database.Factory.CreateDbContextAsync();
        Assert.Equal(oldRevision, (await db.Set<ManagedDocument>().SingleAsync(x => x.Key == "fusion-ingestion-directory")).Revision);
    }

    /// <summary>运维策略使用 LiteDB 的版本控制，SQL 中只有原来的业务文档。</summary>
    [Fact]
    public async Task OperationalPolicyIsDurableInLiteDbAndRejectsStaleWrites() {
        using var storage = new ConfigurationTestStorage();
        await using var relational = new RelationalParcelTestDatabase(); await relational.InitializeAsync();
        var backups = MsOptions.Create(new BackupOptions()); var prebuild = MsOptions.Create(new ShardingPrebuildOptions());
        var policies = new OperationalPolicyService(relational.Factory, backups, prebuild, storage.Store);
        var initial = await policies.ReadAsync(default);
        var saved = await policies.WriteAsync(initial with { BackupIntervalMinutes = 30, PrebuildAheadHours = 48 }, default);
        Assert.NotNull(saved); Assert.Null(await policies.WriteAsync(initial, default));
        Assert.Equal(saved, await new OperationalPolicyService(relational.Factory, backups, prebuild, storage.Store).ReadAsync(default));
        await using var db = await relational.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<ManagedDocument>().AnyAsync(x => x.Key == "operations-policy"));
        Assert.Single(storage.History.Read());
    }

    /// <summary>设计时读取权威快照，旧 JSON 不会重新注入已删除的数组项。</summary>
    [Fact]
    public void ReadOnlyToolsUseExistingLiteDbWithoutCreatingNewDatabases() {
        using var storage = new ConfigurationTestStorage();
        File.WriteAllText(Path.Combine(storage.DirectoryPath, "appsettings.json"), "{\"ConfigurationStorage\":{\"LiteDbPath\":\"settings.db\"},\"WebRequestAuditLog\":{\"ExcludedPathPrefixes\":[\"/stale-one\",\"/stale-two\"]}}");
        storage.Source.Save(storage.Source.Capture().Revision, ConfigurationDocument.Parse("{\"WebRequestAuditLog\":{\"ExcludedPathPrefixes\":[]}}"));
        var values = ConfigurationReadOnlyLoader.Load(storage.DirectoryPath);
        Assert.Empty((System.Text.Json.Nodes.JsonArray)values["WebRequestAuditLog"]!["ExcludedPathPrefixes"]!);
        var missing = Path.Combine(storage.DirectoryPath, "does-not-exist");
        Assert.NotNull(ConfigurationReadOnlyLoader.Load(missing)["Persistence"]);
        Assert.False(Directory.Exists(missing));
    }
}
