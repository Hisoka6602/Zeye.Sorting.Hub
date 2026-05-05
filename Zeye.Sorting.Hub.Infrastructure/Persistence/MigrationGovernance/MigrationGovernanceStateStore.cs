namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移治理运行期状态存储。
/// </summary>
public sealed class MigrationGovernanceStateStore {
    /// <summary>
    /// 并发访问锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 最近一次迁移计划。
    /// </summary>
    private MigrationPlan? _latestPlan;

    /// <summary>
    /// 最近一次执行记录。
    /// </summary>
    private MigrationExecutionRecord? _latestExecutionRecord;

    /// <summary>
    /// 写入最新迁移计划。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    public void SetLatestPlan(MigrationPlan? plan) {
        lock (_syncRoot) {
            _latestPlan = plan;
        }
    }

    /// <summary>
    /// 读取最新迁移计划。
    /// </summary>
    /// <returns>迁移计划。</returns>
    public MigrationPlan? GetLatestPlan() {
        lock (_syncRoot) {
            return _latestPlan;
        }
    }

    /// <summary>
    /// 写入最新执行记录。
    /// </summary>
    /// <param name="record">执行记录。</param>
    public void SetLatestExecutionRecord(MigrationExecutionRecord? record) {
        lock (_syncRoot) {
            _latestExecutionRecord = record;
        }
    }

    /// <summary>
    /// 读取最新执行记录。
    /// </summary>
    /// <returns>执行记录。</returns>
    public MigrationExecutionRecord? GetLatestExecutionRecord() {
        lock (_syncRoot) {
            return _latestExecutionRecord;
        }
    }
}
