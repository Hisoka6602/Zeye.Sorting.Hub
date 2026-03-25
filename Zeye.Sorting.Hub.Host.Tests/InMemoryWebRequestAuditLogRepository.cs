using System.Collections.Concurrent;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Web 请求审计日志仓储测试替身。
/// </summary>
public sealed class InMemoryWebRequestAuditLogRepository : IWebRequestAuditLogRepository, IWebRequestAuditLogQueryRepository {
    /// <summary>
    /// 已写入日志集合。
    /// </summary>
    private readonly ConcurrentQueue<WebRequestAuditLog> _logs = new();

    /// <summary>
    /// 写入次数。
    /// </summary>
    private int _writeCount;

    /// <summary>
    /// 仓储失败错误码。
    /// </summary>
    private string _failureCode = "TEST_REPOSITORY_FAIL";

    /// <summary>
    /// 仓储失败错误信息。
    /// </summary>
    private string _failureMessage = "测试仓储失败";

    /// <summary>
    /// 是否返回失败结果。
    /// </summary>
    public bool ShouldReturnFailure { get; set; }

    /// <summary>
    /// 是否抛出异常。
    /// </summary>
    public bool ShouldThrowException { get; set; }
    /// <summary>
    /// 模拟写入延迟毫秒数（用于验证中间件不阻塞主请求）。
    /// </summary>
    public int AddDelayMilliseconds { get; set; }

    /// <summary>
    /// 获取当前写入次数。
    /// </summary>
    public int WriteCount => _writeCount;

    /// <summary>
    /// 获取当前日志快照。
    /// </summary>
    public IReadOnlyCollection<WebRequestAuditLog> Logs => _logs.ToArray();

    /// <summary>
    /// 设置失败返回信息。
    /// </summary>
    /// <param name="failureMessage">失败信息。</param>
    /// <param name="failureCode">失败码。</param>
    public void ConfigureFailure(string failureMessage, string failureCode = "TEST_REPOSITORY_FAIL") {
        _failureMessage = string.IsNullOrWhiteSpace(failureMessage) ? "测试仓储失败" : failureMessage;
        _failureCode = string.IsNullOrWhiteSpace(failureCode) ? "TEST_REPOSITORY_FAIL" : failureCode;
    }

    /// <summary>
    /// 清理已记录日志。
    /// </summary>
    public void Reset() {
        while (_logs.TryDequeue(out _)) {
        }

        _writeCount = 0;
    }

    /// <summary>
    /// 写入 Web 请求审计日志。
    /// </summary>
    /// <param name="auditLog">审计日志聚合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>仓储结果。</returns>
    public async Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        if (AddDelayMilliseconds > 0) {
            await Task.Delay(AddDelayMilliseconds, cancellationToken);
        }

        Interlocked.Increment(ref _writeCount);

        if (ShouldThrowException) {
            throw new InvalidOperationException("测试仓储抛出异常");
        }

        if (ShouldReturnFailure) {
            return RepositoryResult.Fail(_failureMessage, _failureCode);
        }

        _logs.Enqueue(auditLog);
        return RepositoryResult.Success();
    }

    /// <summary>
    /// 分页查询日志摘要。
    /// </summary>
    /// <param name="filter">查询过滤条件。</param>
    /// <param name="pageRequest">分页参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    public Task<PageResult<WebRequestAuditLogSummaryReadModel>> GetPagedAsync(
        WebRequestAuditLogQueryFilter filter,
        PageRequest pageRequest,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(pageRequest);

        cancellationToken.ThrowIfCancellationRequested();
        var filtered = _logs
            .Where(log => !filter.StartedAtStart.HasValue || log.StartedAt >= filter.StartedAtStart.Value)
            .Where(log => !filter.StartedAtEnd.HasValue || log.StartedAt <= filter.StartedAtEnd.Value)
            .Where(log => !filter.StatusCode.HasValue || log.StatusCode == filter.StatusCode.Value)
            .Where(log => !filter.IsSuccess.HasValue || log.IsSuccess == filter.IsSuccess.Value)
            .Where(log => string.IsNullOrWhiteSpace(filter.TraceId) || string.Equals(log.TraceId, filter.TraceId.Trim(), StringComparison.Ordinal))
            .Where(log => string.IsNullOrWhiteSpace(filter.CorrelationId) || string.Equals(log.CorrelationId, filter.CorrelationId.Trim(), StringComparison.Ordinal))
            .Where(log => string.IsNullOrWhiteSpace(filter.RequestPathKeyword) || log.RequestPath.Contains(filter.RequestPathKeyword.Trim(), StringComparison.Ordinal))
            .OrderByDescending(log => log.StartedAt)
            .ThenByDescending(log => log.Id)
            .ToList();
        var pageNumber = pageRequest.NormalizePageNumber();
        var pageSize = pageRequest.NormalizePageSize();
        var pageItems = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new WebRequestAuditLogSummaryReadModel {
                Id = log.Id,
                TraceId = log.TraceId,
                CorrelationId = log.CorrelationId,
                RequestMethod = log.RequestMethod,
                RequestPath = log.RequestPath,
                StatusCode = log.StatusCode,
                IsSuccess = log.IsSuccess,
                StartedAt = log.StartedAt,
                DurationMs = log.DurationMs
            })
            .ToArray();
        return Task.FromResult(new PageResult<WebRequestAuditLogSummaryReadModel> {
            Items = pageItems,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = filtered.Count
        });
    }

    /// <summary>
    /// 按 Id 查询日志详情。
    /// </summary>
    /// <param name="id">日志主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>详情读模型，不存在时返回 null。</returns>
    public Task<WebRequestAuditLogDetailReadModel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var log = _logs.FirstOrDefault(item => item.Id == id);
        if (log is null) {
            return Task.FromResult<WebRequestAuditLogDetailReadModel?>(null);
        }

        return Task.FromResult<WebRequestAuditLogDetailReadModel?>(new WebRequestAuditLogDetailReadModel {
            WebRequestAuditLogId = log.Detail?.WebRequestAuditLogId ?? 0L,
            Id = log.Id,
            TraceId = log.TraceId,
            CorrelationId = log.CorrelationId,
            SpanId = log.SpanId,
            OperationName = log.OperationName,
            RequestMethod = log.RequestMethod,
            RequestScheme = log.RequestScheme,
            RequestHost = log.RequestHost,
            RequestPort = log.RequestPort,
            RequestPath = log.RequestPath,
            RequestRouteTemplate = log.RequestRouteTemplate,
            UserId = log.UserId,
            UserName = log.UserName,
            IsAuthenticated = log.IsAuthenticated,
            TenantId = log.TenantId,
            RequestPayloadType = log.RequestPayloadType,
            RequestSizeBytes = log.RequestSizeBytes,
            HasRequestBody = log.HasRequestBody,
            IsRequestBodyTruncated = log.IsRequestBodyTruncated,
            ResponsePayloadType = log.ResponsePayloadType,
            ResponseSizeBytes = log.ResponseSizeBytes,
            HasResponseBody = log.HasResponseBody,
            IsResponseBodyTruncated = log.IsResponseBodyTruncated,
            StatusCode = log.StatusCode,
            IsSuccess = log.IsSuccess,
            HasException = log.HasException,
            AuditResourceType = log.AuditResourceType,
            ResourceId = log.ResourceId,
            StartedAt = log.StartedAt,
            EndedAt = log.EndedAt,
            DurationMs = log.DurationMs,
            CreatedAt = log.CreatedAt,
            RequestUrl = log.Detail?.RequestUrl ?? string.Empty,
            RequestQueryString = log.Detail?.RequestQueryString ?? string.Empty,
            RequestHeadersJson = log.Detail?.RequestHeadersJson ?? string.Empty,
            ResponseHeadersJson = log.Detail?.ResponseHeadersJson ?? string.Empty,
            RequestContentType = log.Detail?.RequestContentType ?? string.Empty,
            ResponseContentType = log.Detail?.ResponseContentType ?? string.Empty,
            Accept = log.Detail?.Accept ?? string.Empty,
            Referer = log.Detail?.Referer ?? string.Empty,
            Origin = log.Detail?.Origin ?? string.Empty,
            AuthorizationType = log.Detail?.AuthorizationType ?? string.Empty,
            UserAgent = log.Detail?.UserAgent ?? string.Empty,
            RequestBody = log.Detail?.RequestBody ?? string.Empty,
            ResponseBody = log.Detail?.ResponseBody ?? string.Empty,
            CurlCommand = log.Detail?.CurlCommand ?? string.Empty,
            ErrorMessage = log.Detail?.ErrorMessage ?? string.Empty,
            ExceptionType = log.Detail?.ExceptionType ?? string.Empty,
            ErrorCode = log.Detail?.ErrorCode ?? string.Empty,
            ExceptionStackTrace = log.Detail?.ExceptionStackTrace ?? string.Empty,
            FileMetadataJson = log.Detail?.FileMetadataJson ?? string.Empty,
            HasFileAccess = log.Detail?.HasFileAccess ?? false,
            FileOperationType = log.Detail?.FileOperationType ?? Domain.Enums.AuditLogs.FileOperationType.None,
            FileCount = log.Detail?.FileCount ?? 0,
            FileTotalBytes = log.Detail?.FileTotalBytes ?? 0L,
            ImageMetadataJson = log.Detail?.ImageMetadataJson ?? string.Empty,
            HasImageAccess = log.Detail?.HasImageAccess ?? false,
            ImageCount = log.Detail?.ImageCount ?? 0,
            DatabaseOperationSummary = log.Detail?.DatabaseOperationSummary ?? string.Empty,
            HasDatabaseAccess = log.Detail?.HasDatabaseAccess ?? false,
            DatabaseAccessCount = log.Detail?.DatabaseAccessCount ?? 0,
            DatabaseDurationMs = log.Detail?.DatabaseDurationMs ?? 0L,
            ResourceCode = log.Detail?.ResourceCode ?? string.Empty,
            ResourceName = log.Detail?.ResourceName ?? string.Empty,
            ActionDurationMs = log.Detail?.ActionDurationMs ?? 0L,
            MiddlewareDurationMs = log.Detail?.MiddlewareDurationMs ?? 0L,
            Tags = log.Detail?.Tags ?? string.Empty,
            ExtraPropertiesJson = log.Detail?.ExtraPropertiesJson ?? string.Empty,
            Remark = log.Detail?.Remark ?? string.Empty
        });
    }
}
