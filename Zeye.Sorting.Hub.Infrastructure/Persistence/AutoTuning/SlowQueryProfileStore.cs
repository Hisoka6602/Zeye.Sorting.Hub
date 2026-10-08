using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询画像内存快照存储。
/// </summary>
public sealed class SlowQueryProfileStore : ISlowQueryProfileReader {
    /// <summary>
    /// 样本存取同步锁。
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// 指纹索引。
    /// </summary>
    private readonly Dictionary<string, SlowQueryFingerprint> _fingerprints = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 指纹对应窗口样本。
    /// </summary>
    private readonly Dictionary<string, Queue<SlowQuerySample>> _samplesByFingerprint = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 指纹最近一次命中时间。
    /// </summary>
    private readonly Dictionary<string, DateTime> _lastSeenAtLocalByFingerprint = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 是否启用画像采集。
    /// </summary>
    private readonly bool _isEnabled;

    /// <summary>
    /// 慢查询阈值（毫秒）。
    /// </summary>
    private readonly int _slowQueryThresholdMilliseconds;

    /// <summary>
    /// 保留窗口。
    /// </summary>
    private readonly TimeSpan _window;

    /// <summary>
    /// 最大可追踪指纹数量。
    /// </summary>
    private readonly int _maxFingerprintCount;

    /// <summary>
    /// 单指纹保留的最大样本数量。
    /// </summary>
    private readonly int _maxSampleCountPerFingerprint;

    /// <summary>
    /// 两次全局过期维护之间的记录数量。
    /// </summary>
    private const int MaintenanceRecordInterval = 128;

    /// <summary>
    /// 上次维护后的累计记录数量。
    /// </summary>
    private int _recordsSinceMaintenance;

    /// <summary>仍未完成的执行与读取，仅保留追踪标识和单调时间戳。</summary>
    private readonly ConcurrentDictionary<string, (string TraceId, long Started)> _active = new();
    /// <summary>采集异常与容量淘汰计数。</summary>
    private long _collectionFailures, _capacityEvictions, _expiredSamples;
    /// <summary>归档排队、丢失和恢复计数。</summary>
    private long _archivePending, _archiveDropped, _restoredSamples;
    /// <summary>归档可用标记。</summary>
    private volatile bool _archiveReady;
    /// <summary>有界后台归档队列；热路径仅执行 TryWrite。</summary>
    internal Channel<(SlowQueryFingerprint Fingerprint, SlowQuerySample Sample)> ArchiveQueue { get; }
    /// <summary>归档是否启用。</summary>
    internal bool ArchiveEnabled { get; }
    /// <summary>观测窗口开始时间，用于仅恢复仍有效的历史样本。</summary>
    internal DateTime WindowStart => DateTime.Now - _window;

    /// <summary>
    /// 初始化慢查询画像存储。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    public SlowQueryProfileStore(IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(configuration);
        _isEnabled = AutoTuningConfigurationReader.GetBoolOrDefault(
            configuration,
            AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:IsEnabled"),
            true);
        _slowQueryThresholdMilliseconds = AutoTuningConfigurationReader.GetPositiveIntOrDefault(
            configuration,
            AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryThresholdMilliseconds"),
            500);
        _window = TimeSpan.FromMinutes(Math.Clamp(
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:WindowMinutes"),
                30),
            1,
            1440));
        _maxFingerprintCount = Math.Clamp(
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:MaxFingerprintCount"),
                1000),
            1,
            5000);
        _maxSampleCountPerFingerprint = Math.Clamp(
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:MaxSampleCountPerFingerprint"),
                256),
            1,
            4096);
        ArchiveEnabled = _isEnabled && AutoTuningConfigurationReader.GetBoolOrDefault(configuration,
            AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:ArchiveEnabled"), true);
        ArchiveQueue = Channel.CreateBounded<(SlowQueryFingerprint, SlowQuerySample)>(new BoundedChannelOptions(4096) {
            SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// 记录慢查询样本。
    /// </summary>
    /// <param name="commandText">原始 SQL。</param>
    /// <param name="elapsed">执行耗时。</param>
    /// <param name="affectedRows">影响行数。</param>
    /// <param name="exception">异常。</param>
    public void Record(string commandText, TimeSpan elapsed, int affectedRows = 0, Exception? exception = null) {
        if (!_isEnabled || string.IsNullOrWhiteSpace(commandText)) {
            return;
        }

        var canceled = SlowQueryFailureClassifier.IsCanceled(exception);
        var isError = exception is not null && !canceled;
        var elapsedMilliseconds = elapsed.Ticks / (decimal)TimeSpan.TicksPerMillisecond;
        if (!isError && !canceled && elapsedMilliseconds < _slowQueryThresholdMilliseconds) {
            return;
        }

        var now = DateTime.Now;
        var fingerprint = SlowQueryFingerprintAggregator.Create(commandText);
        var sample = new SlowQuerySample(
            commandText: SlowQueryFingerprintAggregator.SanitizeSql(commandText),
            sqlFingerprint: fingerprint.Fingerprint,
            elapsedMilliseconds: elapsedMilliseconds,
            affectedRows: Math.Max(affectedRows, 0),
            isError: isError,
            isTimeout: SlowQueryFailureClassifier.IsTimeout(exception),
            isDeadlock: SlowQueryFailureClassifier.IsDeadlock(exception),
            occurredTime: now) { Observation = new() { IsCanceled = canceled, ExceptionType = exception?.GetType().FullName ?? "",
                ExecuteMilliseconds = elapsedMilliseconds, CommandCount = 1 } };
        Record(fingerprint, sample);
    }

    /// <summary>
    /// 记录慢查询样本。
    /// </summary>
    /// <param name="sample">慢查询样本。</param>
    public void Record(SlowQuerySample sample) {
        ArgumentNullException.ThrowIfNull(sample);
        if (!_isEnabled) return;
        var fingerprint = SlowQueryFingerprintAggregator.CreateObservation(sample.CommandText, sample.Observation);
        Record(
            fingerprint,
            new SlowQuerySample(
                commandText: SlowQueryFingerprintAggregator.SanitizeSql(sample.CommandText, sample.Observation.Provider),
                sqlFingerprint: fingerprint.Fingerprint,
                elapsedMilliseconds: sample.ElapsedMilliseconds,
                affectedRows: Math.Max(sample.AffectedRows, 0),
                isError: sample.IsError,
                isTimeout: sample.IsTimeout,
                isDeadlock: sample.IsDeadlock,
                occurredTime: sample.OccurredTime) { Observation = sample.Observation });
    }

    /// <summary>
    /// 使用已生成的指纹记录样本，避免重复标准化与哈希。
    /// </summary>
    /// <param name="fingerprint">已生成的慢查询指纹。</param>
    /// <param name="sample">慢查询样本。</param>
    public void Record(SlowQueryFingerprint fingerprint, SlowQuerySample sample) {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(sample);
        if (!_isEnabled) {
            return;
        }

        RecordCore(fingerprint, sample);
        if (ArchiveEnabled) {
            Interlocked.Increment(ref _archivePending);
            if (!ArchiveQueue.Writer.TryWrite((fingerprint, sample))) {
                Interlocked.Decrement(ref _archivePending); Interlocked.Increment(ref _archiveDropped);
            }
        }
    }

    /// <summary>
    /// 获取有界观测窗口中的全部画像，客户端筛选不遗漏排名之外的指纹。
    /// </summary>
    /// <returns>画像快照列表与总量。</returns>
    public (IReadOnlyList<SlowQueryProfileReadModel> Items, int TotalFingerprintCount) GetTopProfiles() {
        List<(SlowQueryFingerprint Fingerprint, SlowQuerySample[] Samples)> snapshotInputs;
        int totalFingerprintCount;
        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            snapshotInputs = new List<(SlowQueryFingerprint, SlowQuerySample[])>(_samplesByFingerprint.Count);
            foreach (var pair in _samplesByFingerprint) {
                if (pair.Value.Count > 0 && _fingerprints.TryGetValue(pair.Key, out var fingerprint)) {
                    snapshotInputs.Add((fingerprint, pair.Value.ToArray()));
                }
            }

            totalFingerprintCount = _samplesByFingerprint.Count;
        }

        var snapshots = snapshotInputs
            .Select(static input => SlowQueryFingerprintAggregator.BuildSnapshot(input.Fingerprint, input.Samples))
            .OrderByDescending(static snapshot => snapshot.P99Milliseconds)
            .ThenByDescending(static snapshot => snapshot.P95Milliseconds)
            .ThenByDescending(static snapshot => snapshot.CallCount)
            .ThenBy(static snapshot => snapshot.Fingerprint, StringComparer.Ordinal)
            // 返回当前有界存储中的全部指纹，前端分页筛选不再受 TopN 隐藏。
            .Take(_maxFingerprintCount)
            .Select(MapToReadModel)
            .ToArray();
        return (snapshots, totalFingerprintCount);
    }

    /// <summary>
    /// 按指纹读取画像快照。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="profile">画像快照。</param>
    /// <returns>是否命中。</returns>
    public bool TryGetProfile(string fingerprint, out SlowQueryProfileReadModel? profile) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        SlowQueryFingerprint? slowQueryFingerprint;
        SlowQuerySample[] samples;
        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            if (!_samplesByFingerprint.TryGetValue(fingerprint, out var queue)
                || queue.Count == 0
                || !_fingerprints.TryGetValue(fingerprint, out slowQueryFingerprint) || slowQueryFingerprint is null) {
                profile = null;
                return false;
            }

            samples = queue.ToArray();
        }

        profile = MapToReadModel(SlowQueryFingerprintAggregator.BuildSnapshot(slowQueryFingerprint, samples));
        return true;
    }

    /// <summary>
    /// 统计可被数据保留策略清理的画像数量（受单次上限保护）。
    /// </summary>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="take">最大统计数量。</param>
    /// <returns>候选数量。</returns>
    public int CountRetentionCandidates(DateTime expireBefore, int take) {
        if (take <= 0) {
            throw new ArgumentOutOfRangeException(nameof(take), "take 必须大于 0。");
        }

        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            return _lastSeenAtLocalByFingerprint
                .Where(pair => pair.Value <= expireBefore)
                .OrderBy(static pair => pair.Value)
                .Take(take)
                .Count();
        }
    }

    /// <summary>
    /// 删除可被数据保留策略清理的画像（受单次上限保护）。
    /// </summary>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="take">最大删除数量。</param>
    /// <returns>已删除数量。</returns>
    public int RemoveRetentionCandidates(DateTime expireBefore, int take) {
        if (take <= 0) {
            throw new ArgumentOutOfRangeException(nameof(take), "take 必须大于 0。");
        }

        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            var fingerprints = _lastSeenAtLocalByFingerprint
                .Where(pair => pair.Value <= expireBefore)
                .OrderBy(static pair => pair.Value)
                .Take(take)
                .Select(static pair => pair.Key)
                .ToArray();
            foreach (var fingerprint in fingerprints) {
                _samplesByFingerprint.Remove(fingerprint);
                _fingerprints.Remove(fingerprint);
                _lastSeenAtLocalByFingerprint.Remove(fingerprint);
            }

            return fingerprints.Length;
        }
    }

    /// <summary>
    /// 获取或创建样本队列。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <returns>样本队列。</returns>
    private Queue<SlowQuerySample> GetOrCreateQueue(SlowQueryFingerprint fingerprint) {
        if (_samplesByFingerprint.TryGetValue(fingerprint.Fingerprint, out var queue)) {
            return queue;
        }

        _fingerprints[fingerprint.Fingerprint] = fingerprint;
        queue = new Queue<SlowQuerySample>();
        _samplesByFingerprint[fingerprint.Fingerprint] = queue;
        return queue;
    }

    /// <summary>
    /// 写入归一化后的慢查询样本。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="sample">样本。</param>
    private void RecordCore(SlowQueryFingerprint fingerprint, SlowQuerySample sample) {
        lock (_sync) {
            var queue = GetOrCreateQueue(fingerprint);
            if (_lastSeenAtLocalByFingerprint.TryGetValue(fingerprint.Fingerprint, out var lastSeen) && sample.OccurredTime < lastSeen) {
                // 后台恢复与新查询可能并发，按时间排序才能正确过期和淘汰最旧样本。
                var ordered = queue.Append(sample).OrderBy(item => item.OccurredTime).ToArray();
                queue.Clear(); foreach (var item in ordered) queue.Enqueue(item);
            }
            else queue.Enqueue(sample);
            var overflowSampleCount = queue.Count - _maxSampleCountPerFingerprint;
            for (var index = 0; index < overflowSampleCount; index++) {
                queue.Dequeue();
                Interlocked.Increment(ref _capacityEvictions);
            }

            _lastSeenAtLocalByFingerprint[fingerprint.Fingerprint] = sample.OccurredTime > lastSeen ? sample.OccurredTime : lastSeen;
            _recordsSinceMaintenance++;
            if (_recordsSinceMaintenance >= MaintenanceRecordInterval) {
                _recordsSinceMaintenance = 0;
                TrimExpiredEntries(DateTime.Now);
            }

            if (_samplesByFingerprint.Count > _maxFingerprintCount) {
                TrimOverflowFingerprints();
            }
        }
    }

    /// <summary>
    /// 裁剪窗口外样本。
    /// </summary>
    /// <param name="now">当前时间。</param>
    private void TrimExpiredEntries(DateTime now) {
        var expireBefore = now - _window;
        var fingerprintsToRemove = new List<string>();
        foreach (var pair in _samplesByFingerprint) {
            while (pair.Value.Count > 0 && pair.Value.Peek().OccurredTime < expireBefore) {
                pair.Value.Dequeue();
                Interlocked.Increment(ref _expiredSamples);
            }

            if (pair.Value.Count == 0) {
                fingerprintsToRemove.Add(pair.Key);
            }
        }

        foreach (var fingerprint in fingerprintsToRemove) {
            _samplesByFingerprint.Remove(fingerprint);
            _fingerprints.Remove(fingerprint);
            _lastSeenAtLocalByFingerprint.Remove(fingerprint);
        }
    }

    /// <summary>
    /// 在超出上限时淘汰最久未更新的指纹。
    /// </summary>
    private void TrimOverflowFingerprints() {
        if (_samplesByFingerprint.Count <= _maxFingerprintCount) {
            return;
        }

        foreach (var fingerprint in _lastSeenAtLocalByFingerprint
                     .OrderBy(static pair => pair.Value)
                     .Select(static pair => pair.Key)
                     .Take(_samplesByFingerprint.Count - _maxFingerprintCount)
                     .ToArray()) {
            Interlocked.Add(ref _capacityEvictions, _samplesByFingerprint[fingerprint].Count);
            _samplesByFingerprint.Remove(fingerprint);
            _fingerprints.Remove(fingerprint);
            _lastSeenAtLocalByFingerprint.Remove(fingerprint);
        }
    }

    /// <summary>
    /// 将内部快照映射为应用层读模型。
    /// </summary>
    /// <param name="snapshot">内部快照。</param>
    /// <returns>应用层读模型。</returns>
    private static SlowQueryProfileReadModel MapToReadModel(SlowQueryProfileSnapshot snapshot) {
        return new SlowQueryProfileReadModel(
            Fingerprint: snapshot.Fingerprint,
            NormalizedSql: snapshot.NormalizedSql,
            SampleSql: snapshot.SampleSql,
            CallCount: snapshot.CallCount,
            AverageElapsedMilliseconds: snapshot.AverageElapsedMilliseconds,
            P95Milliseconds: snapshot.P95Milliseconds,
            P99Milliseconds: snapshot.P99Milliseconds,
            MaxMilliseconds: snapshot.MaxMilliseconds,
            TimeoutCount: snapshot.TimeoutCount,
            ErrorCount: snapshot.ErrorCount,
            DeadlockCount: snapshot.DeadlockCount,
            TotalAffectedRows: snapshot.TotalAffectedRows,
            WindowStartedAtLocal: snapshot.WindowStartedAtLocal,
            WindowEndedAtLocal: snapshot.WindowEndedAtLocal,
            LastOccurredAtLocal: snapshot.LastOccurredAtLocal) { Kind = snapshot.Observation.Kind, Provider = snapshot.Observation.Provider, DatabaseRole = snapshot.Observation.DatabaseRole,
                TraceId = snapshot.Observation.TraceId, SpanId = snapshot.Observation.SpanId, ExceptionType = snapshot.Observation.ExceptionType,
                StatusCode = snapshot.Observation.StatusCode, CommandId = snapshot.Observation.CommandId, LatestCommandCount = snapshot.Observation.CommandCount,
                CanceledCount = snapshot.CanceledCount,
                PartialReadCount = snapshot.PartialReadCount, AverageExecuteMilliseconds = snapshot.AverageExecuteMilliseconds,
                AverageReadMilliseconds = snapshot.AverageReadMilliseconds, AverageConsumerMilliseconds = snapshot.AverageConsumerMilliseconds,
                AverageConnectionMilliseconds = snapshot.AverageConnectionMilliseconds, TotalRowsRead = snapshot.TotalRowsRead };
    }

    /// <summary>登记进行中的操作，数据库阻塞时也能看见尚未完成的观测。</summary>
    internal void Started(string key) {
        if (!_isEnabled || key.Length == 0) return;
        if (_active.Count >= 4096) { CollectionFailed(); return; }
        _active.TryAdd(key, (Activity.Current?.TraceId.ToString() ?? SlowQueryRequestScope.Capture()?.TraceId ?? "", Stopwatch.GetTimestamp()));
    }
    /// <summary>完成或失败均移除活动项。</summary>
    internal void Finished(string key) => _active.TryRemove(key, out _);
    /// <summary>诊断故障不覆盖业务异常，计数对页面可见。</summary>
    internal void CollectionFailed() => Interlocked.Increment(ref _collectionFailures);
    /// <summary>后台归档操作结果。</summary>
    internal void ArchiveCompleted(int count, bool success) { Interlocked.Add(ref _archivePending, -count); if (!success) Interlocked.Add(ref _archiveDropped, count); }
    /// <summary>后台恢复后公布可用状态。</summary>
    internal void ArchiveInitialized(bool ready) => _archiveReady = ready;
    /// <summary>只恢复有效样本，恢复过程不会再次入队或驱动自动调优。</summary>
    internal void Restore(SlowQueryFingerprint fingerprint, SlowQuerySample sample) {
        if (!_isEnabled || sample.OccurredTime < WindowStart) return;
        RecordCore(fingerprint, sample); Interlocked.Increment(ref _restoredSamples);
    }
    /// <inheritdoc />
    public SlowQueryCollectionStatusReadModel GetCollectionStatus() {
        var active = _active.Values.OrderBy(item => item.Started).FirstOrDefault();
        return new() {
            Enabled = _isEnabled, ThresholdMilliseconds = _slowQueryThresholdMilliseconds, WindowMinutes = (int)(_window.Ticks / TimeSpan.TicksPerMinute),
            CapacityEvictions = Interlocked.Read(ref _capacityEvictions), ExpiredSamples = Interlocked.Read(ref _expiredSamples),
            CollectionFailures = Interlocked.Read(ref _collectionFailures), ActiveOperations = _active.Count,
            OldestActiveMilliseconds = active.Started == 0 ? 0m : Stopwatch.GetElapsedTime(active.Started).Ticks / (decimal)TimeSpan.TicksPerMillisecond,
            OldestActiveTraceId = active.TraceId ?? "", ArchiveEnabled = ArchiveEnabled, ArchiveReady = _archiveReady,
            ArchivePending = Interlocked.Read(ref _archivePending), ArchiveDropped = Interlocked.Read(ref _archiveDropped), RestoredSamples = Interlocked.Read(ref _restoredSamples)
        };
    }

}
