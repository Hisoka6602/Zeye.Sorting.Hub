using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>独立设备协议接入，实现认证、耐久接收与图片存储协作。</summary>
public sealed partial class FusionIngestionService : IFusionIngestionGateway {
    /// <summary>数据库上下文工厂。</summary>
    private readonly IDbContextFactory<SortingHubDbContext> _factory;
    /// <summary>启动时验证的接收参数。</summary>
    private readonly FusionIngestionOptions _startupOptions;
    /// <summary>运行时在线接入目录的快照来源。</summary>
    private readonly IFusionRuntimeConfiguration? _runtime;
    /// <summary>获取当前有效的接入限额和服务开关。</summary>
    private FusionIngestionOptions _options => _runtime?.Snapshot.Options ?? _startupOptions;
    /// <summary>不可由来源覆盖的登记目录。</summary>
    private readonly IReadOnlyDictionary<string, FusionSourceOptions> _startupSources;
    /// <summary>获取当前启停及凭据版本一致的工作台目录。</summary>
    private IReadOnlyDictionary<string, FusionSourceOptions> _sources => _runtime?.Snapshot.Sources ?? _startupSources;
    /// <summary>当前进程身份，租约只能由持有者释放。</summary>
    private readonly string _serverId = Guid.NewGuid().ToString("N");
    /// <summary>机器连接的有界身份缓存。</summary>
    private readonly ConcurrentDictionary<string, FusionConnectionLease> _connections = new(StringComparer.Ordinal);
    /// <summary>仅通过 Begin 授权给当前连接的上传身份。</summary>
    private readonly ConcurrentDictionary<string, string> _uploadConnections = new(StringComparer.Ordinal);
    /// <summary>进程内固定大小互斥条带，数据库唯一键与版本令牌继续保护多进程。</summary>
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    /// <summary>服务端生成路径的持久化根目录。</summary>
    private readonly string _imageDirectory;
    /// <summary>协议异常日志，不输出机器凭据或报文原文。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>验证有界参数及独立机器身份，文件目录只由部署配置确定。</summary>
    public FusionIngestionService(IDbContextFactory<SortingHubDbContext> factory, IOptions<FusionIngestionOptions> options, string contentRoot, IFusionRuntimeConfiguration? runtime = null) {
        _factory = factory; _startupOptions = options.Value; _runtime = runtime;
        if (!FusionProtocol.IsIdentity(_options.HubId) || _options.MaxBatchRecords is < 1 or > 100
            || _options.MaxBatchBytes is < 16384 or > 524288 || _options.MaxImageChunkBytes is < 1024 or > 65536
            || _options.MaxImageBytes is < 1 or > 134217728 || _options.MaxPendingImagesPerSource is < 1 or > 1000
            || _options.LeaseSeconds is < 30 or > 600 || _options.UploadRetentionHours is < 1 or > 168
            || _options.DiscoveryPort is < 1024 or > 65535 or 5089 || string.IsNullOrWhiteSpace(_options.ImageDirectory))
            throw new ArgumentException("InvalidFusionConfiguration");
        var sources = _options.Sources ?? [];
        if (sources.Length > 1000 || sources.Select(s => s.SourceInstanceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sources.Length
            || sources.Select(s => FusionProtocol.Hash(s.MachineApiKey)).Distinct(StringComparer.Ordinal).Count() != sources.Length)
            throw new ArgumentException("DuplicateFusionIdentityOrCredential");
        foreach (var source in sources) {
            if (!FusionProtocol.IsIdentity(source.SourceInstanceId) || !FusionProtocol.IsIdentity(source.LineId)
                || !FusionProtocol.IsIdentity(source.TenantId) || !FusionProtocol.IsIdentity(source.StoragePartitionId)
                || source.MachineApiKey.Length is < 32 or > 256 || source.MachineApiKey.Any(char.IsControl)
                || source.WorkstationName.Length > 128 || source.SiteCode is not null && !FusionProtocol.IsIdentity(source.SiteCode)
                || source.DeviceCode is not null && !FusionProtocol.IsIdentity(source.DeviceCode))
                throw new ArgumentException("InvalidFusionSourceConfiguration");
            TimeZoneInfo.FindSystemTimeZoneById(source.TimeZoneId);
        }
        _startupSources = sources.ToDictionary(s => s.SourceInstanceId, StringComparer.Ordinal);
        _imageDirectory = Path.GetFullPath(_options.ImageDirectory, contentRoot);
        if (_imageDirectory.Equals(Path.Combine(contentRoot, "logs"), StringComparison.OrdinalIgnoreCase)
            || _imageDirectory.StartsWith(Path.Combine(contentRoot, "logs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ImageDirectoryCannotUseLogDirectory");
    }

    /// <summary>统一协议可选编码的空字符串与空值。</summary>
    private static string? NullOptional(string? value) => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>按当前来源目录和独立密钥进行固定时间认证。</summary>
    public bool Authenticate(string sourceInstanceId, string credential) => _options.IsEnabled && credential.Length is >= 32 and <= 256
        && _sources.TryGetValue(sourceInstanceId, out var source) && source.Enabled
        && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(credential)), SHA256.HashData(Encoding.UTF8.GetBytes(source.MachineApiKey)));

    /// <summary>从当前连接读取租约，所有业务方法及上传身份共享同一来源边界。</summary>
    private FusionConnectionLease Connection(string connectionId, string? source = null, string? journal = null, string? leaseId = null) {
        if (!_connections.TryGetValue(connectionId, out var connection) || !_options.IsEnabled
            || !_sources.TryGetValue(connection.Source.SourceInstanceId, out var currentSource) || !currentSource.Enabled
            || currentSource.SecurityStamp != connection.Source.SecurityStamp || currentSource.MachineApiKey != connection.Source.MachineApiKey
            || connection.ExpiresAt <= DateTime.Now
            || source is not null && source != connection.Source.SourceInstanceId
            || journal is not null && journal != connection.JournalId || leaseId is not null && leaseId != connection.LeaseId)
            throw new InvalidOperationException("InvalidLease");
        return connection;
    }

    /// <summary>固定大小条带控制同一来源写入竞争，不累积无界键对象。</summary>
    private static async Task<T> LockedAsync<T>(string key, Func<Task<T>> action, CancellationToken cancellationToken) {
        var gate = Gates[(uint)StringComparer.Ordinal.GetHashCode(key) % (uint)Gates.Length];
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }

    /// <summary>注册来源元数据，数据库唯一来源主键和并发版本共同防止克隆部署。</summary>
    public Task<FusionRegistration> RegisterAsync(string connectionId, string authenticatedSource, FusionHello hello, CancellationToken cancellationToken) =>
        LockedAsync(authenticatedSource, async () => {
            if (!_options.IsEnabled || !_sources.TryGetValue(authenticatedSource, out var source) || !source.Enabled || hello.SourceInstanceId != authenticatedSource
                || hello.ProtocolVersion != "1.0" || hello.HubId != _options.HubId || !FusionProtocol.IsHex(hello.JournalId, 32)
                || !FusionProtocol.IsHex(hello.SourceRunId, 32) || !FusionProtocol.IsHex(hello.ProducerSessionId, 32)
                || string.IsNullOrEmpty(hello.ProducerVersion) || hello.ProducerVersion.Length > 256
                || hello.TimeZoneId != source.TimeZoneId || hello.LineId != source.LineId
                || NullOptional(hello.SiteCode) != NullOptional(source.SiteCode) || NullOptional(hello.DeviceCode) != NullOptional(source.DeviceCode)) throw new ArgumentException("RegistrationMismatch");
            if (_connections.TryGetValue(connectionId, out var already)) {
                if (already.Source.SourceInstanceId != authenticatedSource || already.JournalId != hello.JournalId) throw new InvalidOperationException("InvalidLease");
                Connection(connectionId);
                return new FusionRegistration("1.0", _options.HubId, authenticatedSource, hello.JournalId, already.LeaseId,
                    _options.MaxBatchRecords, _options.MaxBatchBytes, _options.MaxImageChunkBytes);
            }
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var row = await db.Set<FusionSourceLease>().AsTracking().SingleOrDefaultAsync(x => x.SourceInstanceId == authenticatedSource, cancellationToken);
            var now = DateTime.Now;
            if (row is not null && row.ExpiresAt > now && row.ConnectionId.Length > 0) throw new InvalidOperationException("SourceInstanceAlreadyConnected");
            if (row is null) { row = new() { SourceInstanceId = authenticatedSource }; db.Add(row); }
            row.LeaseId = Guid.NewGuid().ToString("N"); row.JournalId = hello.JournalId; row.ConnectionId = connectionId;
            row.ServerId = _serverId; row.ExpiresAt = now.AddSeconds(_options.LeaseSeconds); row.LastSeenAt = now; row.Revision++;
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException exception) { Logger.Warn(exception, "Fusion 来源注册竞争，Source={Source}", authenticatedSource); throw new InvalidOperationException("SourceInstanceAlreadyConnected"); }
            _connections[connectionId] = new(source, row.JournalId, row.LeaseId, row.ExpiresAt);
            return new FusionRegistration("1.0", _options.HubId, authenticatedSource, row.JournalId, row.LeaseId,
                _options.MaxBatchRecords, _options.MaxBatchBytes, _options.MaxImageChunkBytes);
        }, cancellationToken);

    /// <summary>心跳指标与租约在同一事务提交，累计舍弃数不允许在同一发送库中回退。</summary>
    public Task<HubHeartbeatReceipt> HeartbeatAsync(string connectionId, FusionHeartbeat heartbeat, CancellationToken cancellationToken) =>
        LockedAsync(heartbeat.SourceInstanceId, async () => {
            var connection = Connection(connectionId, heartbeat.SourceInstanceId, heartbeat.JournalId, heartbeat.LeaseId);
            if (new[] { heartbeat.PendingFacts, heartbeat.RejectedFacts, heartbeat.PendingImages, heartbeat.DroppedUnacknowledgedFacts,
                heartbeat.DroppedUnacknowledgedImages, heartbeat.RetainedBytes }.Any(x => x < 0)) throw new ArgumentException("InvalidHeartbeatCounters");
            var sentAt = FusionProtocol.Local(heartbeat.SentAtUtc, connection.Source.TimeZoneId);
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var row = await db.Set<FusionSourceLease>().AsTracking().SingleAsync(x => x.SourceInstanceId == heartbeat.SourceInstanceId, cancellationToken);
            if (row.ConnectionId != connectionId || row.LeaseId != heartbeat.LeaseId || row.ServerId != _serverId || row.ExpiresAt <= DateTime.Now)
                throw new InvalidOperationException("InvalidLease");
            var key = FusionProtocol.Key(heartbeat.SourceInstanceId, heartbeat.JournalId);
            var report = await db.Set<FusionJournalHeartbeat>().AsTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (report is null) { report = new() { Key = key, SourceInstanceId = heartbeat.SourceInstanceId, JournalId = heartbeat.JournalId }; db.Add(report); }
            if (heartbeat.DroppedUnacknowledgedFacts < report.DroppedUnacknowledgedFacts || heartbeat.DroppedUnacknowledgedImages < report.DroppedUnacknowledgedImages)
                throw new ArgumentException("HeartbeatCountersRegressed");
            report.SentAt = sentAt; report.ReceivedAt = DateTime.Now; report.PendingFacts = heartbeat.PendingFacts;
            report.RejectedFacts = heartbeat.RejectedFacts; report.PendingImages = heartbeat.PendingImages;
            report.DroppedUnacknowledgedFacts = heartbeat.DroppedUnacknowledgedFacts;
            report.DroppedUnacknowledgedImages = heartbeat.DroppedUnacknowledgedImages;
            report.ProtectUnacknowledgedData = heartbeat.ProtectUnacknowledgedData; report.RetainedBytes = heartbeat.RetainedBytes;
            row.LastSeenAt = report.ReceivedAt; row.ExpiresAt = report.ReceivedAt.AddSeconds(_options.LeaseSeconds); row.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            _connections[connectionId] = connection with { ExpiresAt = row.ExpiresAt };
            return new HubHeartbeatReceipt(_options.HubId, heartbeat.SourceInstanceId, heartbeat.JournalId, DateTimeOffset.Now.ToOffset(TimeSpan.Zero));
        }, cancellationToken);

    /// <summary>连接断开释放自身租约及上传授权，未完成文件仍可在重新注册后续传。</summary>
    public async Task DisconnectAsync(string connectionId, CancellationToken cancellationToken) {
        if (!_connections.TryRemove(connectionId, out var connection)) return;
        foreach (var upload in _uploadConnections.Where(x => x.Value == connectionId)) _uploadConnections.TryRemove(upload.Key, out _);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Set<FusionSourceLease>().Where(x => x.SourceInstanceId == connection.Source.SourceInstanceId
            && x.ConnectionId == connectionId && x.LeaseId == connection.LeaseId && x.ServerId == _serverId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ExpiresAt, DateTime.Now).SetProperty(x => x.ConnectionId, "")
                .SetProperty(x => x.Revision, x => x.Revision + 1), cancellationToken);
    }

    /// <summary>登记来源即使尚无包裹也返回，在线依据服务端收到的有效租约。</summary>
    public async Task<IReadOnlyList<FusionSourceStatus>> GetSourcesAsync(CancellationToken cancellationToken) {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var leases = await db.Set<FusionSourceLease>().AsNoTracking().ToDictionaryAsync(x => x.SourceInstanceId, cancellationToken);
        var current = await (from report in db.Set<FusionJournalHeartbeat>()
            join lease in db.Set<FusionSourceLease>() on new { report.SourceInstanceId, report.JournalId } equals new { lease.SourceInstanceId, lease.JournalId }
            select report).AsNoTracking().ToDictionaryAsync(x => x.SourceInstanceId, cancellationToken);
        return _sources.Values.Select(source => {
            leases.TryGetValue(source.SourceInstanceId, out var lease); current.TryGetValue(source.SourceInstanceId, out var report);
            return new FusionSourceStatus(source.SourceInstanceId, source.WorkstationName.Length > 0 ? source.WorkstationName : source.SourceInstanceId,
                _options.HubId, source.TenantId, source.StoragePartitionId, source.LineId, source.SiteCode, source.DeviceCode, lease?.JournalId,
                _options.IsEnabled && source.Enabled && lease is { ConnectionId.Length: > 0 } && lease.ExpiresAt > DateTime.Now, lease?.LastSeenAt,
                report?.PendingFacts ?? 0, report?.RejectedFacts ?? 0, report?.PendingImages ?? 0,
                report?.DroppedUnacknowledgedFacts ?? 0, report?.DroppedUnacknowledgedImages ?? 0,
                report?.ProtectUnacknowledgedData ?? false, report?.RetainedBytes ?? 0);
        }).ToArray();
    }
}
