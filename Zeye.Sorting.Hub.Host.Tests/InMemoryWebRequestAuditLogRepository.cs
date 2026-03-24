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
    public Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _writeCount);

        if (ShouldThrowException) {
            throw new InvalidOperationException("测试仓储抛出异常");
        }

        if (ShouldReturnFailure) {
            return Task.FromResult(RepositoryResult.Fail(_failureMessage, _failureCode));
        }

        _logs.Enqueue(auditLog);
        return Task.FromResult(RepositoryResult.Success());
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
        if (filter is null) {
            throw new ArgumentNullException(nameof(filter));
        }

        if (pageRequest is null) {
            throw new ArgumentNullException(nameof(pageRequest));
        }

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
            StatusCode = log.StatusCode,
            IsSuccess = log.IsSuccess,
            HasException = log.HasException,
            StartedAt = log.StartedAt,
            EndedAt = log.EndedAt,
            DurationMs = log.DurationMs,
            CreatedAt = log.CreatedAt,
            RequestHeadersJson = log.Detail?.RequestHeadersJson ?? string.Empty,
            ResponseHeadersJson = log.Detail?.ResponseHeadersJson ?? string.Empty,
            RequestBody = log.Detail?.RequestBody ?? string.Empty,
            ResponseBody = log.Detail?.ResponseBody ?? string.Empty,
            ErrorMessage = log.Detail?.ErrorMessage ?? string.Empty,
            ExceptionType = log.Detail?.ExceptionType ?? string.Empty,
            ExceptionStackTrace = log.Detail?.ExceptionStackTrace ?? string.Empty
        });
    }
}
