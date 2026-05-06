using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

namespace Zeye.Sorting.Hub.Application.Services.Events;

/// <summary>
/// Outbox 消息分页查询应用服务。
/// </summary>
public sealed class GetOutboxMessagePagedQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Outbox 消息仓储。
    /// </summary>
    private readonly IOutboxMessageRepository _outboxMessageRepository;

    /// <summary>
    /// 初始化 Outbox 消息分页查询应用服务。
    /// </summary>
    /// <param name="outboxMessageRepository">Outbox 消息仓储。</param>
    public GetOutboxMessagePagedQueryService(IOutboxMessageRepository outboxMessageRepository) {
        _outboxMessageRepository = outboxMessageRepository ?? throw new ArgumentNullException(nameof(outboxMessageRepository));
    }

    /// <summary>
    /// 执行分页查询。
    /// </summary>
    /// <param name="request">查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页响应。</returns>
    public async Task<OutboxMessageListResponse> ExecuteAsync(OutboxMessageListRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PageNumber <= 0) {
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), "pageNumber 必须大于 0。");
        }

        if (request.PageSize <= 0 || request.PageSize > 200) {
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), "pageSize 必须在 1~200 之间。");
        }

        var status = ParseOptionalStatus(request.Status);
        try {
            var result = await _outboxMessageRepository.GetPagedAsync(
                new PageRequest {
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                },
                status,
                cancellationToken);

            return new OutboxMessageListResponse {
                Items = result.Items.Select(OutboxMessageContractMapper.ToResponse).ToArray(),
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount
            };
        }
        catch (Exception exception) {
            Logger.Error(exception, "分页查询 Outbox 消息失败，PageNumber={PageNumber}, PageSize={PageSize}, Status={Status}", request.PageNumber, request.PageSize, request.Status);
            throw;
        }
    }

    /// <summary>
    /// 获取 Outbox 健康快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康快照读模型。</returns>
    public async Task<OutboxMessageHealthSnapshotReadModel> GetHealthSnapshotAsync(CancellationToken cancellationToken) {
        try {
            return await _outboxMessageRepository.GetHealthSnapshotAsync(cancellationToken);
        }
        catch (Exception exception) {
            Logger.Error(exception, "读取 Outbox 健康快照失败。");
            throw;
        }
    }

    /// <summary>
    /// 解析可空状态过滤值。
    /// </summary>
    /// <param name="rawStatus">原始状态文本。</param>
    /// <returns>状态过滤值或 null。</returns>
    private static OutboxMessageStatus? ParseOptionalStatus(string? rawStatus) {
        if (string.IsNullOrWhiteSpace(rawStatus)) {
            return null;
        }

        if (!Enum.TryParse<OutboxMessageStatus>(rawStatus, ignoreCase: true, out var status)) {
            throw new ArgumentException("status 仅支持 Pending / Processing / Succeeded / Failed / DeadLettered。", nameof(rawStatus));
        }

        return status;
    }
}
