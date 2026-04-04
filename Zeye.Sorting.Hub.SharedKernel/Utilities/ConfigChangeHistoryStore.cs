namespace Zeye.Sorting.Hub.SharedKernel.Utilities;

/// <summary>
/// 配置变更历史记录条目，保存单次配置变更的快照信息。
/// </summary>
/// <typeparam name="T">配置类型。</typeparam>
/// <param name="Sequence">变更序号（单调递增）。</param>
/// <param name="PreviousValue">变更前配置值（首次记录时为 null）。</param>
/// <param name="CurrentValue">变更后配置值。</param>
/// <param name="EffectiveTime">变更生效本地时间（Kind 应为 Local，禁止传入 UTC 时间；全局禁止 UTC 语义）。</param>
/// <param name="ChangedFields">变更字段摘要描述（由外部调用方提供）。</param>
public sealed record ConfigChangeEntry<T>(
    int Sequence,
    T? PreviousValue,
    T CurrentValue,
    DateTime EffectiveTime,
    string ChangedFields) where T : class;

/// <summary>
/// 配置变更历史存储器，基于环形缓冲保留最近 <see cref="Capacity"/> 条变更快照。
/// 线程安全，支持并发写入与读取，适用于配置热加载的前后值审计与回滚查询。
/// </summary>
/// <typeparam name="T">配置类型。</typeparam>
public sealed class ConfigChangeHistoryStore<T> where T : class {
    /// <summary>
    /// 默认保留条目数量。
    /// </summary>
    public const int DefaultCapacity = 10;

    /// <summary>
    /// 读写互斥锁，保护 <see cref="_buffer"/> 和 <see cref="_sequence"/> 的一致性。
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 环形缓冲数组，容量固定为 <see cref="Capacity"/>。
    /// </summary>
    private readonly ConfigChangeEntry<T>?[] _buffer;

    /// <summary>
    /// 写入指针，循环覆盖旧条目。
    /// </summary>
    private int _head;

    /// <summary>
    /// 当前已写入条目数（上限为 <see cref="Capacity"/>）。
    /// </summary>
    private int _count;

    /// <summary>
    /// 全局单调递增变更序号。
    /// </summary>
    private int _sequence;

    /// <summary>
    /// 缓冲区容量（最多保留的历史条目数）。
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// 初始化 <see cref="ConfigChangeHistoryStore{T}"/>。
    /// </summary>
    /// <param name="capacity">保留的历史条目数量，默认 10。可填写范围：1~100。</param>
    public ConfigChangeHistoryStore(int capacity = DefaultCapacity) {
        if (capacity < 1) {
            capacity = DefaultCapacity;
        }

        Capacity = capacity;
        _buffer = new ConfigChangeEntry<T>?[capacity];
    }

    /// <summary>
    /// 记录一次配置变更快照。
    /// </summary>
    /// <param name="previousValue">变更前配置值（首次调用时可传 null）。</param>
    /// <param name="currentValue">变更后配置值。</param>
    /// <param name="changedFields">变更字段摘要描述（如 "RetentionDays: 2→5"）。</param>
    public void Record(T? previousValue, T currentValue, string changedFields) {
        lock (_lock) {
            _sequence++;
            var entry = new ConfigChangeEntry<T>(
                Sequence: _sequence,
                PreviousValue: previousValue,
                CurrentValue: currentValue,
                EffectiveTime: DateTime.Now,
                ChangedFields: changedFields);
            _buffer[_head] = entry;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) {
                _count++;
            }
        }
    }

    /// <summary>
    /// 获取所有历史快照，按变更序号从旧到新排列。
    /// </summary>
    /// <returns>历史变更条目集合（只读）。</returns>
    public IReadOnlyList<ConfigChangeEntry<T>> GetHistory() {
        lock (_lock) {
            if (_count == 0) {
                return [];
            }

            var result = new ConfigChangeEntry<T>[_count];
            // 环形缓冲：起始读位置为 (_head - _count + Capacity) % Capacity
            var startIndex = (_head - _count + Capacity) % Capacity;
            for (var i = 0; i < _count; i++) {
                result[i] = _buffer[(startIndex + i) % Capacity]!;
            }

            return result;
        }
    }

    /// <summary>
    /// 获取最近一次变更快照（即最新的配置）。
    /// </summary>
    /// <returns>最新变更条目，若无记录则返回 null。</returns>
    public ConfigChangeEntry<T>? GetLatest() {
        lock (_lock) {
            if (_count == 0) {
                return null;
            }

            var latestIndex = (_head - 1 + Capacity) % Capacity;
            return _buffer[latestIndex];
        }
    }
}
