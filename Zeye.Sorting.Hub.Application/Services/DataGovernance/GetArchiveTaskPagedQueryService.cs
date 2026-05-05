using NLog;
using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

namespace Zeye.Sorting.Hub.Application.Services.DataGovernance;

/// <summary>
/// 归档任务分页查询应用服务。
/// </summary>
public sealed class GetArchiveTaskPagedQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 归档任务仓储。
    /// </summary>
    private readonly IArchiveTaskRepository _archiveTaskRepository;

    /// <summary>
    /// 初始化归档任务分页查询应用服务。
    /// </summary>
    /// <param name="archiveTaskRepository">归档任务仓储。</param>
    public GetArchiveTaskPagedQueryService(IArchiveTaskRepository archiveTaskRepository) {
        _archiveTaskRepository = archiveTaskRepository ?? throw new ArgumentNullException(nameof(archiveTaskRepository));
    }

    /// <summary>
    /// 执行分页查询。
    /// </summary>
    /// <param name="request">查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页响应。</returns>
    public async Task<ArchiveTaskListResponse> ExecuteAsync(ArchiveTaskListRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PageNumber <= 0) {
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), "pageNumber 必须大于 0。");
        }

        if (request.PageSize <= 0 || request.PageSize > 200) {
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), "pageSize 必须在 1~200 之间。");
        }

        var status = ParseOptionalEnum<ArchiveTaskStatus>(request.Status, nameof(request.Status), "status 仅支持 Pending / Running / Completed / Failed。");
        var taskType = ParseOptionalEnum<ArchiveTaskType>(request.TaskType, nameof(request.TaskType), "taskType 仅支持 WebRequestAuditLogHistory。");

        try {
            var result = await _archiveTaskRepository.GetPagedAsync(
                new PageRequest {
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                },
                status,
                taskType,
                cancellationToken);
            return new ArchiveTaskListResponse {
                Items = result.Items.Select(ArchiveTaskContractMapper.ToResponse).ToArray(),
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            };
        }
        catch (Exception ex) {
            Logger.Error(ex, "分页查询归档任务失败，PageNumber={PageNumber}, PageSize={PageSize}, Status={Status}, TaskType={TaskType}", request.PageNumber, request.PageSize, request.Status, request.TaskType);
            throw;
        }
    }

    /// <summary>
    /// 解析可空枚举过滤值。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <param name="rawValue">原始文本。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="errorMessage">错误消息。</param>
    /// <returns>枚举值或 null。</returns>
    private static TEnum? ParseOptionalEnum<TEnum>(string? rawValue, string paramName, string errorMessage)
        where TEnum : struct, Enum {
        if (string.IsNullOrWhiteSpace(rawValue)) {
            return null;
        }

        if (!Enum.TryParse<TEnum>(rawValue, ignoreCase: true, out var enumValue)) {
            throw new ArgumentException(errorMessage, paramName);
        }

        return enumValue;
    }
}
