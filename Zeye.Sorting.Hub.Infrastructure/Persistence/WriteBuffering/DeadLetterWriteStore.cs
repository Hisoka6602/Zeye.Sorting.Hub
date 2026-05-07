using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// Parcel 死信存储。
/// </summary>
public sealed class DeadLetterWriteStore {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 并发访问锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 死信队列。
    /// </summary>
    private readonly Queue<DeadLetterWriteEntry> _entries = [];

    /// <summary>
    /// 累计被覆盖的死信数量。
    /// </summary>
    private long _droppedCount;

    /// <summary>
    /// 初始化死信存储。
    /// </summary>
    /// <param name="capacity">容量上限。</param>
    public DeadLetterWriteStore(int capacity) {
        Capacity = Math.Max(1, capacity);
    }

    /// <summary>
    /// 死信容量上限。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 当前死信数量。
    /// </summary>
    public int Count {
        get {
            lock (_syncRoot) {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// 累计被覆盖的死信数量。
    /// </summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <summary>
    /// 添加死信记录。
    /// </summary>
    /// <param name="entry">死信记录。</param>
    public void Add(DeadLetterWriteEntry entry) {
        var shouldLogDropped = false;
        long droppedCount = 0;
        lock (_syncRoot) {
            if (_entries.Count >= Capacity) {
                _entries.Dequeue();
                droppedCount = Interlocked.Increment(ref _droppedCount);
                shouldLogDropped = true;
            }

            _entries.Enqueue(entry);
        }

        if (shouldLogDropped) {
            Logger.Warn("Parcel 死信存储已满，覆盖最旧记录。DroppedCount={DroppedCount}", droppedCount);
        }
    }

    /// <summary>
    /// 获取当前死信快照。
    /// </summary>
    /// <returns>死信快照。</returns>
    public IReadOnlyList<DeadLetterWriteEntry> GetSnapshot() {
        lock (_syncRoot) {
            return _entries.ToArray();
        }
    }

    /// <summary>
    /// 统计已过期的死信数量（受单次上限保护）。
    /// </summary>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="take">最大统计数量。</param>
    /// <returns>候选数量。</returns>
    public int CountExpired(DateTime expireBefore, int take) {
        if (take <= 0) {
            throw new ArgumentOutOfRangeException(nameof(take), "take 必须大于 0。");
        }

        lock (_syncRoot) {
            return _entries
                .Where(entry => entry.FailedAtLocal <= expireBefore)
                .Take(take)
                .Count();
        }
    }

    /// <summary>
    /// 删除已过期的死信记录（受单次上限保护）。
    /// </summary>
    /// <param name="expireBefore">过期截止时间。</param>
    /// <param name="take">最大删除数量。</param>
    /// <returns>已删除数量。</returns>
    public int RemoveExpired(DateTime expireBefore, int take) {
        if (take <= 0) {
            throw new ArgumentOutOfRangeException(nameof(take), "take 必须大于 0。");
        }

        lock (_syncRoot) {
            var removedCount = 0;
            var initialCount = _entries.Count;
            for (var index = 0; index < initialCount; index++) {
                var entry = _entries.Dequeue();
                if (removedCount < take && entry.FailedAtLocal <= expireBefore) {
                    removedCount++;
                    continue;
                }

                _entries.Enqueue(entry);
            }

            return removedCount;
        }
    }
}
