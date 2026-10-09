using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>后台批量持久化和恢复采样，热路径无数据库或文件访问，背压与失败对页面可见。</summary>
public sealed class SlowQueryArchiveWorker : BackgroundService {
    /// <summary>共享画像与队列。</summary>
    private readonly SlowQueryProfileStore _store;
    /// <summary>归档文件路径，保存在已挂载的治理目录。</summary>
    internal string ArchivePath { get; }
    /// <summary>有限历史保留天数。</summary>
    private readonly int _retentionDays;
    /// <summary>归档样本上限，避免诊断无限占用磁盘。</summary>
    private readonly int _maximumSamples;
    /// <summary>归档异常日志；任何归档故障都不得阻断业务请求。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>最近一次归档维护时间。</summary>
    private DateTime _maintainedAt;
    /// <summary>后台存储配置及归档路径。</summary>
    public SlowQueryArchiveWorker(SlowQueryProfileStore store, IConfiguration configuration, IHostEnvironment? environment = null) {
        _store = store;
        var directory = configuration["Persistence:AutoTuning:SlowQueryProfile:ArchiveDirectory"] ?? "governance-artifacts/slow-queries";
        ArchivePath = Path.Combine(Path.GetFullPath(directory, environment?.ContentRootPath ?? Directory.GetCurrentDirectory()), "observations.db");
        _retentionDays = Math.Clamp(AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:SlowQueryProfile:ArchiveRetentionDays", 7), 1, 365);
        _maximumSamples = Math.Clamp(AutoTuningConfigurationReader.GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:SlowQueryProfile:ArchiveMaximumSamples", 50000), 100, 500000);
    }
    /// <summary>恢复当前窗口的有效采样，旧样本保留在有界归档中。</summary>
    internal async Task InitializeAsync(CancellationToken token) {
        await EnsureArchiveAsync(token);
        await using var db = new SlowQueryArchiveDbContext(ArchivePath);
        var start = _store.WindowStart;
        var rows = await db.Set<SlowQueryArchiveRow>().AsNoTracking().Where(row => row.OccurredAt >= start)
            .OrderByDescending(row => row.Id).Take(_maximumSamples).ToListAsync(token);
        foreach (var row in rows.OrderBy(row => row.OccurredAt)) {
            try {
                var sample = JsonSerializer.Deserialize<SlowQuerySample>(row.Payload);
                if (sample is null || string.IsNullOrWhiteSpace(sample.CommandText) || sample.Observation is null
                    || sample.Observation.Provider is null || sample.Observation.Kind is null || sample.Observation.DatabaseRole is null
                    || sample.ElapsedMilliseconds < 0m || sample.Observation.RowsRead < 0)
                    throw new JsonException("归档样本缺少必需的诊断字段或计数无效。");
                _store.Restore(new(row.Fingerprint, SlowQueryFingerprintAggregator.NormalizeSql(sample.CommandText, sample.Observation.Provider)), sample);
            }
            catch (JsonException exception) { _store.CollectionFailed(); Logger.Warn(exception, "跳过损坏的慢查询归档样本，ArchiveRow={ArchiveRow}", row.Id); }
        }
        _store.ArchiveInitialized(true);
    }
    /// <summary>启动取消或初始化尚未完成时，停止流程仍可幂等建好归档库。</summary>
    private async Task EnsureArchiveAsync(CancellationToken token) {
        Directory.CreateDirectory(Path.GetDirectoryName(ArchivePath)!);
        await using var db = new SlowQueryArchiveDbContext(ArchivePath);
        await db.Database.EnsureCreatedAsync(token);
    }
    /// <summary>后台批量写入，使用 EF Core，不向业务数据库增加诊断写操作。</summary>
    internal async Task FlushAsync(IReadOnlyList<(SlowQueryFingerprint Fingerprint, SlowQuerySample Sample)> batch, CancellationToken token) {
        await using var db = new SlowQueryArchiveDbContext(ArchivePath);
        if (batch.Count == 0) return;
        var rows = batch.Select(item => new SlowQueryArchiveRow {
            Fingerprint = item.Fingerprint.Fingerprint, OccurredAt = item.Sample.OccurredTime,
            Payload = JsonSerializer.Serialize(item.Sample)
        }).ToArray();
        // 写入结果未知后重试仍保持幂等；时间窗口索引限制查重扫描范围。
        var earliest = rows.Min(row => row.OccurredAt); var latest = rows.Max(row => row.OccurredAt);
        var existing = await db.Set<SlowQueryArchiveRow>().AsNoTracking().Where(row => row.OccurredAt >= earliest && row.OccurredAt <= latest)
            .Select(row => row.Payload).ToListAsync(token);
        var written = existing.ToHashSet(StringComparer.Ordinal);
        db.AddRange(rows.Where(row => !written.Contains(row.Payload)));
        await db.SaveChangesAsync(token);
        if (DateTime.Now - _maintainedAt < TimeSpan.FromMinutes(1)) return;
        try {
            var expired = DateTime.Now.AddDays(-_retentionDays);
            await db.Set<SlowQueryArchiveRow>().Where(row => row.OccurredAt < expired).ExecuteDeleteAsync(token);
            var cutoff = await db.Set<SlowQueryArchiveRow>().OrderByDescending(row => row.Id).Skip(_maximumSamples).Select(row => (long?)row.Id).FirstOrDefaultAsync(token);
            if (cutoff.HasValue) await db.Set<SlowQueryArchiveRow>().Where(row => row.Id <= cutoff.Value).ExecuteDeleteAsync(token);
            _maintainedAt = DateTime.Now;
        }
        catch (Exception exception) { _store.CollectionFailed(); Logger.Warn(exception, "归档已写入，但慢查询历史轮转失败，下一批重试维护。"); }
    }
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!_store.ArchiveEnabled) return;
        while (!stoppingToken.IsCancellationRequested) {
            try { await InitializeAsync(stoppingToken); break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) {
                _store.ArchiveInitialized(false); Logger.Error(exception, "慢查询历史归档初始化失败，将重试；内存采集继续运行。");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        while (await _store.ArchiveQueue.Reader.WaitToReadAsync(stoppingToken)) {
            // 合并短时间内的慢样本，避免一次样本一次文件事务。
            if (_store.ArchiveQueue.Reader.Count < 128) await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            var batch = new List<(SlowQueryFingerprint, SlowQuerySample)>(128);
            while (batch.Count < 128 && _store.ArchiveQueue.Reader.TryRead(out var sample)) batch.Add(sample);
            var success = false;
            Exception? lastFailure = null;
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            for (var attempt = 0; attempt < 3 && !success; attempt++) {
                try { await FlushAsync(batch, budget.Token); success = true; _store.ArchiveInitialized(true); }
                catch (Exception exception) {
                    lastFailure = exception; _store.ArchiveInitialized(false);
                    if (budget.IsCancellationRequested) break;
                    // 已出队样本使用独立预算完成归档，停机时也不会跳过完成计数。
                    if (attempt < 2) {
                        try { await Task.Delay(TimeSpan.FromMilliseconds(200), budget.Token); }
                        catch (OperationCanceledException) when (budget.IsCancellationRequested) { break; }
                    }
                }
            }
            if (!success) Logger.Error(lastFailure, "慢查询历史归档重试失败，丢失数量={Count}。", batch.Count);
            _store.ArchiveCompleted(batch.Count, success);
        }
    }
    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken) {
        _store.ArchiveQueue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
        var batch = new List<(SlowQueryFingerprint, SlowQuerySample)>();
        while (_store.ArchiveQueue.Reader.TryRead(out var sample)) batch.Add(sample);
        if (batch.Count == 0) return;
        try { await EnsureArchiveAsync(cancellationToken); await FlushAsync(batch, cancellationToken); _store.ArchiveCompleted(batch.Count, true); _store.ArchiveInitialized(true); }
        catch (Exception exception) { _store.ArchiveCompleted(batch.Count, false); Logger.Error(exception, "停止时慢查询归档未能写完。"); }
    }
}
