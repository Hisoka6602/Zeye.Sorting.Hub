using NLog;
using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.DataGovernance;

/// <summary>
/// 重试归档任务应用服务。
/// </summary>
public sealed class RetryArchiveTaskCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务仓储。
    /// </summary>
    private readonly IArchiveTaskRepository _archiveTaskRepository;

    /// <summary>
    /// 初始化重试归档任务应用服务。
    /// </summary>
    /// <param name="archiveTaskRepository">归档任务仓储。</param>
    public RetryArchiveTaskCommandService(IArchiveTaskRepository archiveTaskRepository) {
        _archiveTaskRepository = archiveTaskRepository ?? throw new ArgumentNullException(nameof(archiveTaskRepository));
    }

    /// <summary>
    /// 将终态任务重新投入执行队列。
    /// </summary>
    /// <param name="id">任务主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>任务响应；不存在时返回 null。</returns>
    public async Task<ArchiveTaskResponse?> ExecuteAsync(long id, CancellationToken cancellationToken) {
        if (id <= 0) {
            throw new ArgumentOutOfRangeException(nameof(id), "id 必须大于 0。");
        }

        var archiveTask = await _archiveTaskRepository.GetByIdAsync(id, cancellationToken);
        if (archiveTask is null) {
            return null;
        }

        if (archiveTask.Status is ArchiveTaskStatus.Pending or ArchiveTaskStatus.Running) {
            throw new InvalidOperationException("仅已完成或已失败的任务允许重试。");
        }

        archiveTask.Requeue();
        var result = await _archiveTaskRepository.UpdateAsync(archiveTask, cancellationToken);
        if (!result.IsSuccess) {
            Logger.Error("重试归档任务失败，TaskId={TaskId}, ErrorMessage={ErrorMessage}", id, result.ErrorMessage);
            throw new InvalidOperationException(result.ErrorMessage ?? "重试归档任务失败。");
        }

        return ArchiveTaskContractMapper.ToResponse(archiveTask);
    }
}
