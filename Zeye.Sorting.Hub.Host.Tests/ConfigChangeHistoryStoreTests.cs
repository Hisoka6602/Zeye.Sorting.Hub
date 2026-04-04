using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// ConfigChangeHistoryStore 单元测试。
/// </summary>
public sealed class ConfigChangeHistoryStoreTests {

    /// <summary>
    /// 验证场景：初始状态下历史为空。
    /// </summary>
    [Fact]
    public void GetHistory_WhenNoRecords_ReturnsEmpty() {
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>();
        Assert.Empty(store.GetHistory());
    }

    /// <summary>
    /// 验证场景：GetLatest 在无记录时返回 null。
    /// </summary>
    [Fact]
    public void GetLatest_WhenNoRecords_ReturnsNull() {
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>();
        Assert.Null(store.GetLatest());
    }

    /// <summary>
    /// 验证场景：记录一条变更后，GetLatest 返回该条目，GetHistory 返回包含一条。
    /// </summary>
    [Fact]
    public void Record_SingleEntry_CanBeRetrievedViaGetLatestAndGetHistory() {
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>();
        var settings = new LogCleanupSettings { RetentionDays = 5 };

        store.Record(null, settings, "RetentionDays: (null)→5");

        var latest = store.GetLatest();
        Assert.NotNull(latest);
        Assert.Equal(1, latest.Sequence);
        Assert.Null(latest.PreviousValue);
        Assert.Equal(settings, latest.CurrentValue);
        Assert.Equal("RetentionDays: (null)→5", latest.ChangedFields);

        var history = store.GetHistory();
        Assert.Single(history);
    }

    /// <summary>
    /// 验证场景：记录多条时，历史按序从旧到新排列，序号单调递增。
    /// </summary>
    [Fact]
    public void Record_MultipleEntries_HistoryOrderedBySequenceAscending() {
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>(capacity: 5);
        var s1 = new LogCleanupSettings { RetentionDays = 1 };
        var s2 = new LogCleanupSettings { RetentionDays = 2 };
        var s3 = new LogCleanupSettings { RetentionDays = 3 };

        store.Record(null, s1, "init");
        store.Record(s1, s2, "RetentionDays: 1→2");
        store.Record(s2, s3, "RetentionDays: 2→3");

        var history = store.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal(1, history[0].Sequence);
        Assert.Equal(2, history[1].Sequence);
        Assert.Equal(3, history[2].Sequence);
        Assert.Equal(s3, store.GetLatest()!.CurrentValue);
    }

    /// <summary>
    /// 验证场景：条目数超过容量时，旧条目被环形覆盖，仅保留最近 capacity 条。
    /// </summary>
    [Fact]
    public void Record_ExceedCapacity_OlderEntriesEvicted() {
        const int capacity = 3;
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>(capacity);

        for (var i = 1; i <= 5; i++) {
            store.Record(null, new LogCleanupSettings { RetentionDays = i }, $"step {i}");
        }

        var history = store.GetHistory();
        Assert.Equal(capacity, history.Count);
        // 最旧的应为序号 3（4、5 是最新的 capacity 条）
        Assert.Equal(3, history[0].Sequence);
        Assert.Equal(4, history[1].Sequence);
        Assert.Equal(5, history[2].Sequence);
        // GetLatest 返回序号最大的
        Assert.Equal(5, store.GetLatest()!.Sequence);
    }

    /// <summary>
    /// 验证场景：容量超出上限时 clamp 到 MaxCapacity，低于 1 时 clamp 到 1。
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(101, ConfigChangeHistoryStore<LogCleanupSettings>.MaxCapacity)]
    [InlineData(1000, ConfigChangeHistoryStore<LogCleanupSettings>.MaxCapacity)]
    public void Constructor_CapacityOutOfRange_IsClamped(int inputCapacity, int expectedCapacity) {
        var store = new ConfigChangeHistoryStore<LogCleanupSettings>(inputCapacity);
        Assert.Equal(expectedCapacity, store.Capacity);
    }

    /// <summary>
    /// 验证场景：Record 传入值为 record with {} 副本后，后续修改原始对象不影响历史条目（LogCleanupService 行为验证）。
    /// </summary>
    [Fact]
    public void LogCleanupService_OnSettingsChanged_SnapshotIsIsolatedFromLaterChanges() {
        var v1 = new LogCleanupSettings { RetentionDays = 2, Enabled = true };
        var v2 = new LogCleanupSettings { RetentionDays = 7, Enabled = true };
        var v3 = new LogCleanupSettings { RetentionDays = 30, Enabled = true };

        var settingsMonitor = new TestOptionsMonitor<LogCleanupSettings>(v1);
        var changeHistory = new ConfigChangeHistoryStore<LogCleanupSettings>();
        _ = new LogCleanupService(
            new SafeExecutor(),
            settingsMonitor,
            new TestObservability(),
            changeHistory);

        settingsMonitor.Update(v2);
        settingsMonitor.Update(v3);

        // 第一次变更的快照 PreviousValue 应仍为 v1 的值，第二次变更不影响第一次快照
        var history = changeHistory.GetHistory();
        Assert.Equal(2, history.Count);
        Assert.Equal(2, history[0].PreviousValue!.RetentionDays);
        Assert.Equal(7, history[0].CurrentValue.RetentionDays);
        Assert.Equal(7, history[1].PreviousValue!.RetentionDays);
        Assert.Equal(30, history[1].CurrentValue.RetentionDays);
    }

    /// <summary>
    /// 验证场景：LogCleanupService 在配置变更时触发快照记录，历史中可查到前后值。
    /// </summary>
    [Fact]
    public void LogCleanupService_OnSettingsChanged_RecordsBeforeAfterSnapshot() {
        var initialSettings = new LogCleanupSettings { RetentionDays = 2, Enabled = true };
        var updatedSettings = new LogCleanupSettings { RetentionDays = 7, Enabled = true };

        var settingsMonitor = new TestOptionsMonitor<LogCleanupSettings>(initialSettings);
        var changeHistory = new ConfigChangeHistoryStore<LogCleanupSettings>();
        _ = new LogCleanupService(
            new SafeExecutor(),
            settingsMonitor,
            new TestObservability(),
            changeHistory);

        // 触发热加载变更
        settingsMonitor.Update(updatedSettings);

        var history = changeHistory.GetHistory();
        Assert.Single(history);
        var entry = history[0];
        Assert.Equal(initialSettings, entry.PreviousValue);
        Assert.Equal(updatedSettings, entry.CurrentValue);
        Assert.Contains("RetentionDays", entry.ChangedFields);
    }
}
