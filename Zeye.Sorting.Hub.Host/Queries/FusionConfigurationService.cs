using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>按版本持久化接入目录，密钥加密保存，运行读取使用不可变快照。</summary>
public sealed class FusionConfigurationService : IFusionRuntimeConfiguration {
    /// <summary>记录目录保存及输入校验异常，不输出配置正文或机器密钥。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>接入目录在管理文档表中的固定键。</summary>
    private const string DocumentKey = "fusion-ingestion-directory";
    /// <summary>使用网页字段名称的序列化设置。</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>串行化当前进程内的目录写入。</summary>
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    /// <summary>创建持久化管理数据库上下文。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>生产环境使用 LiteDB；旧工具调用保留原数据库读取语义。</summary>
    private readonly IConfigurationDocumentStore? _configurations;
    /// <summary>加密保存各来源的独立机器密钥。</summary>
    private readonly IDataProtector _protector;
    /// <summary>部署固定的 Hub 身份及图片目录。</summary>
    private readonly FusionIngestionOptions _deployment;
    /// <summary>最近一次有效的运行配置快照。</summary>
    private FusionRuntimeSnapshot _snapshot;
    /// <summary>获取当前版本的原子配置快照。</summary>
    public FusionRuntimeSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>初始化部署配置及凭据保护器。</summary>
    public FusionConfigurationService(IDbContextFactory<SortingHubDbContext> factory,
        IOptions<FusionIngestionOptions> options, IDataProtectionProvider protection, IConfigurationDocumentStore? configurations = null) {
        _factory = factory;
        _configurations = configurations;
        _protector = protection.CreateProtector("Zeye.Sorting.Hub.FusionCredentials.v1");
        _deployment = Clone(options.Value);
        Validate(_deployment);
        _snapshot = Build(Clone(_deployment), 0);
    }

    /// <summary>复制配置，避免改变已经发布的快照。</summary>
    private static FusionIngestionOptions Clone(FusionIngestionOptions value) =>
        JsonSerializer.Deserialize<FusionIngestionOptions>(JsonSerializer.Serialize(value, Json), Json)!;
    /// <summary>构建同一版本的接入配置和来源索引。</summary>
    private static FusionRuntimeSnapshot Build(FusionIngestionOptions value, int revision) =>
        new(value, value.Sources.ToDictionary(x => x.SourceInstanceId, StringComparer.Ordinal), revision);
    /// <summary>仅发布较新的配置版本，防止读取回退。</summary>
    private void Publish(FusionIngestionOptions value, int revision) {
        var next = Build(value, revision);
        while (true) {
            var current = Snapshot;
            if (current.Revision >= revision) return;
            if (ReferenceEquals(Interlocked.CompareExchange(ref _snapshot, next, current), current)) return;
        }
    }
    /// <summary>加密独立机器密钥后序列化目录。</summary>
    private string Encode(FusionIngestionOptions value) {
        var stored = Clone(value);
        foreach (var source in stored.Sources) source.MachineApiKey = _protector.Protect(source.MachineApiKey);
        return JsonSerializer.Serialize(stored, Json);
    }
    /// <summary>解密保存的目录，并保护部署固定的 Hub 身份。</summary>
    private FusionIngestionOptions Decode(string text) {
        var stored = JsonSerializer.Deserialize<FusionIngestionOptions>(text, Json) ?? throw new InvalidDataException("InvalidFusionDirectory");
        foreach (var source in stored.Sources) source.MachineApiKey = _protector.Unprotect(source.MachineApiKey);
        // Storage location and Hub ownership remain deployment-owned; never retarget existing facts.
        if (stored.HubId != _deployment.HubId) throw new InvalidDataException("FusionHubIdentityChanged");
        stored.ImageDirectory = _deployment.ImageDirectory;
        Validate(stored);
        return stored;
    }

    /// <summary>读取最新持久化版本并保留有效快照。</summary>
    public async Task RefreshAsync(CancellationToken ct) {
        if (_configurations is not null) {
            ct.ThrowIfCancellationRequested();
            var stored = _configurations.Read(DocumentKey);
            if (stored is not null && stored.Revision != Snapshot.Revision) Publish(Decode(stored.Json), stored.Revision);
            return;
        }
        await using var db = await _factory.CreateDbContextAsync(ct);
        var doc = await db.Set<ManagedDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == DocumentKey, ct);
        if (doc is not null && doc.Revision != Snapshot.Revision) Publish(Decode(doc.Json), doc.Revision);
    }
    /// <summary>首次导入部署目录，后续启动读取数据库目录。</summary>
    public async Task InitializeAsync(CancellationToken ct) {
        await WriteGate.WaitAsync(ct);
        try {
            if (_configurations is not null) {
                var stored = _configurations.Read(DocumentKey);
                if (stored is null) {
                    var initial = Clone(_deployment);
                    foreach (var source in initial.Sources) source.SecurityStamp = Guid.NewGuid().ToString("N");
                    stored = _configurations.Write(DocumentKey, Encode(initial), 0) ?? _configurations.Read(DocumentKey)!;
                }
                Publish(Decode(stored.Json), stored.Revision);
                RemoveImportedCredentialSeeds();
                return;
            }
            await using var db = await _factory.CreateDbContextAsync(ct);
            var doc = await db.Set<ManagedDocument>().AsTracking().SingleOrDefaultAsync(x => x.Key == DocumentKey, ct);
            if (doc is null) {
                var initial = Clone(_deployment);
                foreach (var source in initial.Sources) source.SecurityStamp = Guid.NewGuid().ToString("N");
                doc = new() { Key = DocumentKey, Json = Encode(initial), Revision = 1, ModifiedAt = DateTime.Now };
                db.Add(doc);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException exception) {
                    Logger.Debug(exception, "Fusion 初始目录保存失败，重新读取其他实例已提交的目录。");
                    await RefreshAsync(ct); if (Snapshot.Revision == 0) throw; return;
                }
            }
            Publish(Decode(doc.Json), doc.Revision);
        } finally { WriteGate.Release(); }
    }

    /// <summary>接入目录加密保存后删除旧 JSON 导入的明文密钥种子，不再保留第二份来源配置。</summary>
    private void RemoveImportedCredentialSeeds() {
        if (_configurations is not LiteDbConfigurationStore store) return;
        for (var attempt = 0; attempt < 3; attempt++) {
            var current = store.ReadRuntime();
            if (current["FusionIngestion"] is not System.Text.Json.Nodes.JsonObject fusion
                || fusion["Sources"] is not System.Text.Json.Nodes.JsonArray sources || sources.Count == 0) return;
            var revision = ConfigurationDocument.Revision(current);
            fusion["Sources"] = new System.Text.Json.Nodes.JsonArray();
            if (store.WriteRuntime(revision, current)) return;
        }
        throw new InvalidOperationException("接入目录已导入，但旧明文种子存在并发修改，请重启后重试。");
    }

    /// <summary>接入目录版本及公开配置快照。</summary>
    public async Task<FusionConfigurationView> ReadAsync(CancellationToken ct) {
        await RefreshAsync(ct);
        var snapshot = Snapshot;
        await using var db = await _factory.CreateDbContextAsync(ct);
        var used = (await db.Set<FusionSourceLease>().AsNoTracking().Select(x => x.SourceInstanceId).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        return new(snapshot.Revision, FusionSettings.From(snapshot.Options), snapshot.Options.HubId,
            snapshot.Options.ImageDirectory, snapshot.Options.Sources.Select(x => SourceView(x, used.Contains(x.SourceInstanceId))).ToArray());
    }
    /// <summary>不包含机器密钥的工作台显示信息。</summary>
    private static FusionSourceView SourceView(FusionSourceOptions source, bool identityLocked) => new(
        source.SourceInstanceId, source.WorkstationName, source.Enabled, source.TenantId, source.StoragePartitionId,
        source.LineId, source.SiteCode, source.DeviceCode, source.TimeZoneId, identityLocked);

    /// <summary>按预期版本保存目录；新安全戳立即撤销旧连接，关系库租约随后更新。</summary>
    private async Task<FusionRuntimeSnapshot?> MutateAsync(int revision,
        Func<FusionIngestionOptions, SortingHubDbContext, Task<string[]>> change, CancellationToken ct) {
        if (revision < 1) throw new ArgumentException("配置版本无效，请刷新后重试。");
        await WriteGate.WaitAsync(ct);
        try {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var doc = _configurations is null
                ? await db.Set<ManagedDocument>().AsTracking().SingleAsync(x => x.Key == DocumentKey, ct)
                : _configurations.Read(DocumentKey) ?? throw new InvalidOperationException("接入配置尚未初始化。");
            if (doc.Revision != revision) { Publish(Decode(doc.Json), doc.Revision); return null; }
            var next = Decode(doc.Json);
            var revoked = await change(next, db);
            Validate(next);
            if (revoked.Length > 0) {
                var leases = await db.Set<FusionSourceLease>().AsTracking().Where(x => revoked.Contains(x.SourceInstanceId)).ToListAsync(ct);
                foreach (var lease in leases) { lease.ConnectionId = ""; lease.ExpiresAt = DateTime.Now; lease.Revision++; }
            }
            if (_configurations is not null) {
                var saved = _configurations.Write(DocumentKey, Encode(next), revision);
                if (saved is null) { await RefreshAsync(ct); return null; }
                Publish(next, saved.Revision);
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException exception) {
                    Logger.Warn(exception, "接入配置已持久化，新安全戳已撤销旧连接；租约状态将在过期后恢复。");
                }
                return Build(next, saved.Revision);
            }
            doc.Json = Encode(next); doc.Revision++; doc.ModifiedAt = DateTime.Now;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException exception) {
                Logger.Debug(exception, "Fusion 目录发生并发修改，当前修改未覆盖已提交配置。");
                await RefreshAsync(ct); return null;
            }
            Publish(next, doc.Revision);
            return Build(next, doc.Revision);
        } finally { WriteGate.Release(); }
    }

    /// <summary>更新接入服务和传输设置并在线生效。</summary>
    public Task<FusionRuntimeSnapshot?> WriteSettingsAsync(int revision, FusionSettings settings, CancellationToken ct) =>
        MutateAsync(revision, (next, _) => {
            var securityChange = next.IsEnabled != settings.IsEnabled || next.AllowInsecureHttp != settings.AllowInsecureHttp;
            settings.Apply(next);
            var revoked = securityChange ? next.Sources.Select(x => x.SourceInstanceId).ToArray() : [];
            foreach (var source in next.Sources.Where(x => revoked.Contains(x.SourceInstanceId))) source.SecurityStamp = Guid.NewGuid().ToString("N");
            return Task.FromResult(revoked);
        }, ct);

    /// <summary>配对信息及保存后的目录版本。</summary>
    public async Task<FusionPairingResult?> CreateSourceAsync(int revision, FusionSourceWrite request, CancellationToken ct) {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var result = await MutateAsync(revision, (next, _) => {
            if (next.Sources.Any(x => x.SourceInstanceId.Equals(request.SourceInstanceId, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("来源标识已存在，每个 Fusion 必须使用独立标识。");
            var source = request.ToOptions(); source.MachineApiKey = key; source.SecurityStamp = Guid.NewGuid().ToString("N");
            next.Sources = [.. next.Sources, source];
            return Task.FromResult(Array.Empty<string>());
        }, ct);
        return result is null ? null : Pairing(result, request.SourceInstanceId, key);
    }

    /// <summary>更新工作台名称和启停状态，保护已接入身份。</summary>
    public Task<FusionRuntimeSnapshot?> UpdateSourceAsync(int revision, string id, FusionSourceWrite request, CancellationToken ct) =>
        MutateAsync(revision, async (next, db) => {
            var old = next.Sources.SingleOrDefault(x => x.SourceInstanceId == id) ?? throw new ArgumentException("工作台不存在。");
            if (id != request.SourceInstanceId) throw new ArgumentException("来源标识不可修改，请新增工作台。");
            var updated = request.ToOptions();
            var changedIdentity = old.TenantId != updated.TenantId || old.StoragePartitionId != updated.StoragePartitionId
                || old.LineId != updated.LineId || old.SiteCode != updated.SiteCode || old.DeviceCode != updated.DeviceCode || old.TimeZoneId != updated.TimeZoneId;
            if (changedIdentity && await db.Set<FusionSourceLease>().AnyAsync(x => x.SourceInstanceId == id, ct))
                throw new ArgumentException("工作台已接入，业务归属和设备身份不可改写；请新增来源标识。");
            updated.MachineApiKey = old.MachineApiKey;
            updated.SecurityStamp = old.Enabled != updated.Enabled || changedIdentity ? Guid.NewGuid().ToString("N") : old.SecurityStamp;
            next.Sources = next.Sources.Select(x => x.SourceInstanceId == id ? updated : x).ToArray();
            return updated.SecurityStamp != old.SecurityStamp ? [id] : [];
        }, ct);

    /// <summary>配对信息及保存后的目录版本。</summary>
    public async Task<FusionPairingResult?> RotateKeyAsync(int revision, string id, CancellationToken ct) {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var result = await MutateAsync(revision, (next, _) => {
            var source = next.Sources.SingleOrDefault(x => x.SourceInstanceId == id) ?? throw new ArgumentException("工作台不存在。");
            source.MachineApiKey = key; source.SecurityStamp = Guid.NewGuid().ToString("N");
            return Task.FromResult(new[] { id });
        }, ct);
        return result is null ? null : Pairing(result, id, key);
    }
    /// <summary>配对信息及保存后的目录版本。</summary>
    private static FusionPairingResult Pairing(FusionRuntimeSnapshot snapshot, string id, string key) {
        var source = snapshot.Sources[id]; var options = snapshot.Options;
        return new(snapshot.Revision, new("zeye.fusion-hub.pairing", "1.0", options.HubId, id, key,
            source.LineId, source.TimeZoneId, source.SiteCode ?? "", source.DeviceCode ?? "", options.AdvertisedEndpoint,
            options.AllowInsecureHttp, options.DiscoveryPort));
    }
    /// <summary>身份检查结果和不匹配字段。</summary>
    public FusionConfigurationCheck Check(string authenticatedSource, FusionConfigurationProbe probe) {
        var snapshot = Snapshot;
        if (!snapshot.Options.IsEnabled || !snapshot.Sources.TryGetValue(authenticatedSource, out var source) || !source.Enabled)
            throw new ArgumentException("RegistrationMismatch");
        var mismatches = new List<string>();
        if (probe.HubId != snapshot.Options.HubId) mismatches.Add("HubId");
        if (probe.SourceInstanceId != authenticatedSource) mismatches.Add("SourceInstanceId");
        if (probe.LineId != source.LineId) mismatches.Add("LineId");
        if (probe.TimeZoneId != source.TimeZoneId) mismatches.Add("TimeZoneId");
        if (Optional(probe.SiteCode) != source.SiteCode) mismatches.Add("SiteCode");
        if (Optional(probe.DeviceCode) != source.DeviceCode) mismatches.Add("DeviceCode");
        return new(mismatches.Count == 0, snapshot.Options.HubId, authenticatedSource, mismatches.ToArray());
    }
    /// <summary>统一可选编码的空串与空值。</summary>
    public static string? Optional(string? value) => string.IsNullOrEmpty(value) ? null : value;
    /// <summary>验证身份、限额、时区和独立发现端口。</summary>
    public static void Validate(FusionIngestionOptions options) {
        if (!FusionProtocol.IsIdentity(options.HubId) || options.MaxBatchRecords is < 1 or > 100
            || options.MaxBatchBytes is < 16384 or > 524288 || options.MaxImageChunkBytes is < 1024 or > 65536
            || options.MaxImageBytes is < 1 or > 134217728 || options.MaxPendingImagesPerSource is < 1 or > 1000
            || options.LeaseSeconds is < 30 or > 600 || options.UploadRetentionHours is < 1 or > 168
            || options.DiscoveryPort is < 1024 or > 65535 or 5089 || string.IsNullOrWhiteSpace(options.ImageDirectory))
            throw new ArgumentException("接入参数超出范围；UDP 5089 保留给分拣机，建议使用 47651。");
        if (!string.IsNullOrEmpty(options.AdvertisedEndpoint) && !FusionDiscoveryService.IsEndpoint(options.AdvertisedEndpoint, options.AllowInsecureHttp)
            || options.DiscoveryEnabled && string.IsNullOrEmpty(options.AdvertisedEndpoint))
            throw new ArgumentException("请填写有效对外 SignalR 地址，以 /hubs/fusion-ingestion 结尾；HTTP 需显式允许。");
        if (options.Sources.Length > 1000 || options.Sources.Select(x => x.SourceInstanceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Sources.Length
            || options.Sources.Select(x => x.MachineApiKey).Distinct(StringComparer.Ordinal).Count() != options.Sources.Length)
            throw new ArgumentException("工作台数量不得超过 1000，来源标识和机器密钥必须独立。");
        foreach (var source in options.Sources) {
            source.SiteCode = Optional(source.SiteCode); source.DeviceCode = Optional(source.DeviceCode);
            if (!FusionProtocol.IsIdentity(source.SourceInstanceId) || !FusionProtocol.IsIdentity(source.LineId)
                || !FusionProtocol.IsIdentity(source.TenantId) || !FusionProtocol.IsIdentity(source.StoragePartitionId)
                || source.MachineApiKey is null || source.MachineApiKey.Length is < 32 or > 256 || source.MachineApiKey.Any(char.IsControl)
                || source.WorkstationName.Length > 128 || source.SiteCode is not null && !FusionProtocol.IsIdentity(source.SiteCode)
                || source.DeviceCode is not null && !FusionProtocol.IsIdentity(source.DeviceCode))
                throw new ArgumentException("工作台身份无效：编码为 1～96 位字母、数字、点、下划线或连字符。");
            try { TimeZoneInfo.FindSystemTimeZoneById(source.TimeZoneId); }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) {
                Logger.Warn(exception, "Fusion 工作台的业务时区无效，Source={Source}", source.SourceInstanceId);
                throw new ArgumentException("业务时区无效。", exception);
            }
        }
    }
}
