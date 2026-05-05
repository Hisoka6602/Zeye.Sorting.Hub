using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

/// <summary>
/// 归档任务检查点存储器。
/// </summary>
public sealed class DataArchiveCheckpointStore {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务仓储。
    /// </summary>
    private readonly IArchiveTaskRepository _archiveTaskRepository;

    /// <summary>
    /// 初始化检查点存储器。
    /// </summary>
    /// <param name="archiveTaskRepository">归档任务仓储。</param>
    public DataArchiveCheckpointStore(IArchiveTaskRepository archiveTaskRepository) {
        _archiveTaskRepository = archiveTaskRepository ?? throw new ArgumentNullException(nameof(archiveTaskRepository));
    }

    /// <summary>
    /// 加载归档任务。
    /// </summary>
    /// <param name="taskId">任务主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档任务；不存在时返回 null。</returns>
    public Task<ArchiveTask?> LoadAsync(long taskId, CancellationToken cancellationToken) {
        return _archiveTaskRepository.GetByIdAsync(taskId, cancellationToken);
    }

    /// <summary>
    /// 标记任务进入执行中。
    /// </summary>
    /// <param name="taskId">任务主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档任务；不存在时返回 null。</returns>
    public async Task<ArchiveTask?> MarkRunningAsync(long taskId, CancellationToken cancellationToken) {
        var archiveTask = await _archiveTaskRepository.GetByIdAsync(taskId, cancellationToken);
        if (archiveTask is null) {
            return null;
        }

        archiveTask.MarkRunning();
        await PersistAsync(archiveTask, cancellationToken);
        return archiveTask;
    }

    /// <summary>
    /// 标记任务执行完成。
    /// </summary>
    /// <param name="taskId">任务主键。</param>
    /// <param name="plannedItemCount">计划数量。</param>
    /// <param name="planSummary">摘要。</param>
    /// <param name="checkpointPayload">检查点载荷。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task MarkCompletedAsync(
        long taskId,
        int plannedItemCount,
        string planSummary,
        string checkpointPayload,
        CancellationToken cancellationToken) {
        var archiveTask = await _archiveTaskRepository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"未找到 Id 为 {taskId} 的归档任务。");
        archiveTask.MarkCompleted(plannedItemCount, planSummary, checkpointPayload);
        await PersistAsync(archiveTask, cancellationToken);
    }

    /// <summary>
    /// 标记任务执行失败。
    /// </summary>
    /// <param name="taskId">任务主键。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task MarkFailedAsync(long taskId, string failureMessage, CancellationToken cancellationToken) {
        var archiveTask = await _archiveTaskRepository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"未找到 Id 为 {taskId} 的归档任务。");
        archiveTask.MarkFailed(failureMessage);
        await PersistAsync(archiveTask, cancellationToken);
    }

    /// <summary>
    /// 持久化归档任务变更。
    /// </summary>
    /// <param name="archiveTask">归档任务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task PersistAsync(ArchiveTask archiveTask, CancellationToken cancellationToken) {
        var result = await _archiveTaskRepository.UpdateAsync(archiveTask, cancellationToken);
        if (!result.IsSuccess) {
            Logger.Error("归档任务检查点写入失败，TaskId={TaskId}, ErrorMessage={ErrorMessage}", archiveTask.Id, result.ErrorMessage);
            throw new InvalidOperationException(result.ErrorMessage ?? "归档任务检查点写入失败。");
        }
    }
}
