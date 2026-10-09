using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;

namespace Zeye.Sorting.Hub.Application.Services.AuditLogs;

/// <summary>
/// 分页查询 Web 请求审计日志应用服务。
/// </summary>
public sealed class GetWebRequestAuditLogPagedQueryService {
    /// <summary>
    /// 审计查询默认时间跨度。
    /// </summary>
    private static readonly TimeSpan DefaultQueryRange = TimeSpan.FromDays(1);

    /// <summary>
    /// 审计查询允许的最大时间跨度。
    /// </summary>
    private static readonly TimeSpan MaxQueryRange = TimeSpan.FromDays(31);

    /// <summary>
    /// 普通分页允许的最大页码。
    /// </summary>
    private const int MaxPageNumber = 1000;
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly NLog.Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 审计日志查询仓储。
    /// </summary>
    private readonly IWebRequestAuditLogQueryRepository _webRequestAuditLogQueryRepository;

    /// <summary>
    /// 初始化分页查询服务。
    /// </summary>
    /// <param name="webRequestAuditLogQueryRepository">审计日志查询仓储。</param>
    public GetWebRequestAuditLogPagedQueryService(IWebRequestAuditLogQueryRepository webRequestAuditLogQueryRepository) {
        _webRequestAuditLogQueryRepository = webRequestAuditLogQueryRepository ?? throw new ArgumentNullException(nameof(webRequestAuditLogQueryRepository));
    }

    /// <summary>
    /// 执行分页查询。
    /// </summary>
    /// <param name="request">分页查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页响应。</returns>
    public async Task<WebRequestAuditLogListResponse> ExecuteAsync(
        WebRequestAuditLogListRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        try {
            var normalizedRange = NormalizeTimeRange(request.StartedAtStart, request.StartedAtEnd);
            var filter = new WebRequestAuditLogQueryFilter {
                StartedAtStart = normalizedRange.Start,
                StartedAtEnd = normalizedRange.End,
                StatusCode = request.StatusCode,
                IsSuccess = request.IsSuccess,
                TraceId = request.TraceId,
                CorrelationId = request.CorrelationId,
                RequestPathKeyword = request.RequestPathKeyword
            };
            var pageRequest = new PageRequest {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                IncludeTotalCount = request.IncludeTotalCount
            };
            var pageResult = await _webRequestAuditLogQueryRepository.GetPagedAsync(filter, pageRequest, cancellationToken);
            var items = pageResult.Items
                .Select(WebRequestAuditLogContractMapper.ToListItem)
                .ToArray();
            return new WebRequestAuditLogListResponse {
                Items = items,
                PageNumber = pageResult.PageNumber,
                PageSize = pageResult.PageSize,
                TotalCount = pageResult.TotalCount,
                HasTotalCount = request.IncludeTotalCount
            };
        }
        catch (Exception exception) {
            NLogLogger.Error(
                exception,
                "分页查询 Web 请求审计日志失败，PageNumber={PageNumber}, PageSize={PageSize}, StartedAtStart={StartedAtStart}, StartedAtEnd={StartedAtEnd}, StatusCode={StatusCode}, IsSuccess={IsSuccess}, TraceId={TraceId}, CorrelationId={CorrelationId}, RequestPathKeyword={RequestPathKeyword}",
                request.PageNumber,
                request.PageSize,
                request.StartedAtStart,
                request.StartedAtEnd,
                request.StatusCode,
                request.IsSuccess,
                request.TraceId,
                request.CorrelationId,
                request.RequestPathKeyword);
            throw;
        }
    }

    /// <summary>
    /// 校验分页请求参数。
    /// </summary>
    /// <param name="request">分页请求参数。</param>
    private static void ValidateRequest(WebRequestAuditLogListRequest request) {
        Guard.ThrowIfZeroOrNegative(request.PageNumber, nameof(request.PageNumber), "页码必须大于 0。", "分页查询 Web 请求审计日志");
        Guard.ThrowIfZeroOrNegative(request.PageSize, nameof(request.PageSize), "页大小必须大于 0。", "分页查询 Web 请求审计日志");
        if (request.PageNumber > MaxPageNumber) {
            throw new ArgumentOutOfRangeException(nameof(request), $"页码不能超过 {MaxPageNumber}，更深结果请改用时间范围缩小查询。");
        }

        if (request.StartedAtStart.HasValue && request.StartedAtEnd.HasValue && request.StartedAtEnd.Value < request.StartedAtStart.Value) {
            NLogLogger.Warn(
                "分页查询 Web 请求审计日志参数非法，StartedAtStart={StartedAtStart}, StartedAtEnd={StartedAtEnd}",
                request.StartedAtStart,
                request.StartedAtEnd);
            throw new ArgumentException("startedAtEnd 不能早于 startedAtStart。", nameof(request));
        }

        if (request.StatusCode.HasValue && request.StatusCode.Value <= 0) {
            NLogLogger.Warn("分页查询 Web 请求审计日志参数非法，StatusCode={StatusCode}", request.StatusCode);
            throw new ArgumentOutOfRangeException(nameof(request), "statusCode 必须大于 0。");
        }
    }

    /// <summary>
    /// 补齐并限制审计查询时间范围。
    /// </summary>
    /// <param name="start">可选起始时间。</param>
    /// <param name="end">可选结束时间。</param>
    /// <returns>规范化后的闭区间。</returns>
    private static (DateTime Start, DateTime End) NormalizeTimeRange(DateTime? start, DateTime? end) {
        var normalizedEnd = end ?? DateTime.Now;
        var normalizedStart = start ?? normalizedEnd - DefaultQueryRange;
        if (normalizedEnd < normalizedStart) {
            throw new ArgumentException("startedAtEnd 不能早于 startedAtStart。");
        }

        if (normalizedEnd - normalizedStart > MaxQueryRange) {
            throw new ArgumentOutOfRangeException(nameof(start), "审计查询时间跨度不能超过 31 天。");
        }

        return (normalizedStart, normalizedEnd);
    }
}
