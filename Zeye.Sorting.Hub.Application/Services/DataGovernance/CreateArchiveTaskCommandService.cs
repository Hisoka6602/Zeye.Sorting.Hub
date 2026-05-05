using NLog;
using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.DataGovernance;

/// <summary>
/// 创建归档任务应用服务。
/// </summary>
public sealed class CreateArchiveTaskCommandService {
    /// <summary>
    /// 归档保留天数最小值。
    /// </summary>
    private const int MinRetentionDays = ArchiveTask.MinRetentionDays;

    /// <summary>
    /// 归档保留天数最大值。
    /// </summary>
    private const int MaxRetentionDays = ArchiveTask.MaxRetentionDays;

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务仓储。
    /// </summary>
    private readonly IArchiveTaskRepository _archiveTaskRepository;

    /// <summary>
    /// 初始化创建归档任务应用服务。
    /// </summary>
    /// <param name="archiveTaskRepository">归档任务仓储。</param>
    public CreateArchiveTaskCommandService(IArchiveTaskRepository archiveTaskRepository) {
        _archiveTaskRepository = archiveTaskRepository ?? throw new ArgumentNullException(nameof(archiveTaskRepository));
    }

    /// <summary>
    /// 创建新的 dry-run 归档任务。
    /// </summary>
    /// <param name="request">创建请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档任务响应。</returns>
    public async Task<ArchiveTaskResponse> ExecuteAsync(ArchiveTaskCreateRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRetentionDays(request.RetentionDays);
        var taskType = ParseTaskType(request.TaskType);
        var archiveTask = ArchiveTask.CreateDryRun(taskType, request.RetentionDays, request.RequestedBy, request.Remark);
        var result = await _archiveTaskRepository.AddAsync(archiveTask, cancellationToken);
        if (!result.IsSuccess) {
            Logger.Error("创建归档任务失败，TaskType={TaskType}, RetentionDays={RetentionDays}, ErrorMessage={ErrorMessage}", request.TaskType, request.RetentionDays, result.ErrorMessage);
            throw new InvalidOperationException(result.ErrorMessage ?? "创建归档任务失败。");
        }

        return ArchiveTaskContractMapper.ToResponse(archiveTask);
    }

    /// <summary>
    /// 解析归档任务类型。
    /// </summary>
    /// <param name="rawTaskType">原始任务类型文本。</param>
    /// <returns>归档任务类型。</returns>
    private static ArchiveTaskType ParseTaskType(string rawTaskType) {
        if (string.IsNullOrWhiteSpace(rawTaskType)
            || !Enum.TryParse<ArchiveTaskType>(rawTaskType, ignoreCase: true, out var taskType)) {
            throw new ArgumentException("taskType 仅支持 WebRequestAuditLogHistory。", nameof(rawTaskType));
        }

        return taskType;
    }

    /// <summary>
    /// 校验归档保留天数范围。
    /// </summary>
    /// <param name="retentionDays">保留天数。</param>
    private static void ValidateRetentionDays(int retentionDays) {
        if (retentionDays is < MinRetentionDays or > MaxRetentionDays) {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), $"retentionDays 必须在 {MinRetentionDays}~{MaxRetentionDays} 之间。");
        }
    }
}
