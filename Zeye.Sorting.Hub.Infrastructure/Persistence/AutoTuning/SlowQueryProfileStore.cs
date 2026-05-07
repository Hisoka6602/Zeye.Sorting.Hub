using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

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
    /// 列表接口返回的 TopN。
    /// </summary>
    private readonly int _topN;

    /// <summary>
    /// 最大可追踪指纹数量。
    /// </summary>
    private readonly int _maxFingerprintCount;

    /// <summary>
    /// 单指纹保留的最大样本数量。
    /// </summary>
    private readonly int _maxSampleCountPerFingerprint;

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
        _topN = Math.Clamp(
            AutoTuningConfigurationReader.GetPositiveIntOrDefault(
                configuration,
                AutoTuningConfigurationReader.BuildAutoTuningKey("SlowQueryProfile:TopN"),
                50),
            1,
            Math.Min(_maxFingerprintCount, 200));
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

        var isError = exception is not null;
        if (!isError && elapsed.TotalMilliseconds < _slowQueryThresholdMilliseconds) {
            return;
        }

        var now = DateTime.Now;
        var fingerprint = SlowQueryFingerprintAggregator.Create(commandText);
        var sample = new SlowQuerySample(
            commandText: commandText,
            sqlFingerprint: fingerprint.Fingerprint,
            elapsedMilliseconds: elapsed.TotalMilliseconds,
            affectedRows: Math.Max(affectedRows, 0),
            isError: isError,
            isTimeout: IsTimeoutException(exception),
            isDeadlock: IsDeadlockException(exception),
            occurredTime: now);
        Record(sample);
    }

    /// <summary>
    /// 记录慢查询样本。
    /// </summary>
    /// <param name="sample">慢查询样本。</param>
    public void Record(SlowQuerySample sample) {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_sync) {
            var fingerprint = SlowQueryFingerprintAggregator.Create(sample.CommandText);
            var normalizedSample = new SlowQuerySample(
                commandText: sample.CommandText,
                sqlFingerprint: fingerprint.Fingerprint,
                elapsedMilliseconds: sample.ElapsedMilliseconds,
                affectedRows: Math.Max(sample.AffectedRows, 0),
                isError: sample.IsError,
                isTimeout: sample.IsTimeout,
                isDeadlock: sample.IsDeadlock,
                occurredTime: sample.OccurredTime);
            var queue = GetOrCreateQueue(fingerprint);
            queue.Enqueue(normalizedSample);
            var overflowSampleCount = queue.Count - _maxSampleCountPerFingerprint;
            for (var index = 0; index < overflowSampleCount; index++) {
                queue.Dequeue();
            }

            _lastSeenAtLocalByFingerprint[fingerprint.Fingerprint] = normalizedSample.OccurredTime;
            TrimExpiredEntries(DateTime.Now);
            TrimOverflowFingerprints();
        }
    }

    /// <summary>
    /// 获取 TopN 画像快照。
    /// </summary>
    /// <returns>画像快照列表与总量。</returns>
    public (IReadOnlyList<SlowQueryProfileReadModel> Items, int TotalFingerprintCount) GetTopProfiles() {
        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            var snapshots = BuildSnapshotsCore()
                .OrderByDescending(static snapshot => snapshot.P99Milliseconds)
                .ThenByDescending(static snapshot => snapshot.P95Milliseconds)
                .ThenByDescending(static snapshot => snapshot.CallCount)
                .ThenBy(static snapshot => snapshot.Fingerprint, StringComparer.Ordinal)
                .Take(_topN)
                .Select(MapToReadModel)
                .ToArray();
            return (snapshots, _samplesByFingerprint.Count);
        }
    }

    /// <summary>
    /// 按指纹读取画像快照。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="snapshot">画像快照。</param>
    /// <returns>是否命中。</returns>
    public bool TryGetProfile(string fingerprint, out SlowQueryProfileReadModel? profile) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        lock (_sync) {
            TrimExpiredEntries(DateTime.Now);
            if (!_samplesByFingerprint.TryGetValue(fingerprint, out var queue)
                || queue.Count == 0
                || !_fingerprints.TryGetValue(fingerprint, out var slowQueryFingerprint)) {
                profile = null;
                return false;
            }

            profile = MapToReadModel(SlowQueryFingerprintAggregator.BuildSnapshot(slowQueryFingerprint, queue.ToArray()));
            return true;
        }
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
    /// 裁剪窗口外样本。
    /// </summary>
    /// <param name="now">当前时间。</param>
    private void TrimExpiredEntries(DateTime now) {
        var expireBefore = now - _window;
        var fingerprintsToRemove = new List<string>();
        foreach (var pair in _samplesByFingerprint) {
            while (pair.Value.Count > 0 && pair.Value.Peek().OccurredTime < expireBefore) {
                pair.Value.Dequeue();
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
            _samplesByFingerprint.Remove(fingerprint);
            _fingerprints.Remove(fingerprint);
            _lastSeenAtLocalByFingerprint.Remove(fingerprint);
        }
    }

    /// <summary>
    /// 构建全部有效快照。
    /// </summary>
    /// <returns>快照序列。</returns>
    private IReadOnlyList<SlowQueryProfileSnapshot> BuildSnapshotsCore() {
        var snapshots = new List<SlowQueryProfileSnapshot>(_samplesByFingerprint.Count);
        foreach (var pair in _samplesByFingerprint) {
            if (pair.Value.Count == 0 || !_fingerprints.TryGetValue(pair.Key, out var fingerprint)) {
                continue;
            }

            snapshots.Add(SlowQueryFingerprintAggregator.BuildSnapshot(fingerprint, pair.Value.ToArray()));
        }

        return snapshots;
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
            LastOccurredAtLocal: snapshot.LastOccurredAtLocal);
    }

    /// <summary>
    /// 判断异常是否属于超时。
    /// </summary>
    /// <param name="exception">异常。</param>
    /// <returns>是否超时。</returns>
    private static bool IsTimeoutException(Exception? exception) {
        if (exception is null) {
            return false;
        }

        if (exception is TimeoutException) {
            return true;
        }

        return DatabaseProviderOperations.TryGetProviderErrorNumber(exception, out var number)
            && (number == -2 || number == 3024);
    }

    /// <summary>
    /// 判断异常是否属于死锁。
    /// </summary>
    /// <param name="exception">异常。</param>
    /// <returns>是否死锁。</returns>
    private static bool IsDeadlockException(Exception? exception) {
        if (exception is null) {
            return false;
        }

        return DatabaseProviderOperations.TryGetProviderErrorNumber(exception, out var number)
            && (number == 1205 || number == 1213);
    }
}
