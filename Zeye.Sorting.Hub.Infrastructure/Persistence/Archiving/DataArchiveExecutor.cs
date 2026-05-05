using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

/// <summary>
/// 数据归档 dry-run 执行器。
/// </summary>
public sealed class DataArchiveExecutor {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档计划器。
    /// </summary>
    private readonly DataArchivePlanner _dataArchivePlanner;

    /// <summary>
    /// 检查点存储器。
    /// </summary>
    private readonly DataArchiveCheckpointStore _checkpointStore;

    /// <summary>
    /// 初始化数据归档执行器。
    /// </summary>
    /// <param name="dataArchivePlanner">归档计划器。</param>
    /// <param name="checkpointStore">检查点存储器。</param>
    public DataArchiveExecutor(
        DataArchivePlanner dataArchivePlanner,
        DataArchiveCheckpointStore checkpointStore) {
        _dataArchivePlanner = dataArchivePlanner ?? throw new ArgumentNullException(nameof(dataArchivePlanner));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
    }

    /// <summary>
    /// 执行指定归档任务。
    /// </summary>
    /// <param name="archiveTask">已领取的归档任务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task ExecuteAsync(ArchiveTask archiveTask, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(archiveTask);
        try {
            var plan = await _dataArchivePlanner.BuildPlanAsync(archiveTask, cancellationToken);
            await _checkpointStore.MarkCompletedAsync(archiveTask.Id, plan.PlannedItemCount, plan.PlanSummary, plan.CheckpointPayload, cancellationToken);
            Logger.Info("归档 dry-run 任务执行完成，TaskId={TaskId}, PlannedItemCount={PlannedItemCount}", archiveTask.Id, plan.PlannedItemCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Warn("归档 dry-run 任务收到取消信号，TaskId={TaskId}", archiveTask.Id);
            throw;
        }
        catch (Exception ex) {
            Logger.Error(ex, "归档 dry-run 任务执行失败，TaskId={TaskId}", archiveTask.Id);
            await _checkpointStore.MarkFailedAsync(archiveTask.Id, ex.Message, cancellationToken);
        }
    }
}
