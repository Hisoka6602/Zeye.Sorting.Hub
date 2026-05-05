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
        lock (_syncRoot) {
            if (_entries.Count >= Capacity) {
                _entries.Dequeue();
                var droppedCount = Interlocked.Increment(ref _droppedCount);
                Logger.Warn("Parcel 死信存储已满，覆盖最旧记录。DroppedCount={DroppedCount}", droppedCount);
            }

            _entries.Enqueue(entry);
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
}
