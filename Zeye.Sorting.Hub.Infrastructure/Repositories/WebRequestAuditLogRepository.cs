using Microsoft.EntityFrameworkCore;
using NLog;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {

    /// <summary>
    /// Web 请求审计日志仓储实现（热表+冷表同事务写入）。
    /// </summary>
    public sealed class WebRequestAuditLogRepository : RepositoryBase<WebRequestAuditLog, SortingHubDbContext>, IWebRequestAuditLogRepository, IWebRequestAuditLogQueryRepository {
        /// <summary>
        /// NLog 日志器（静态，无需 DI 注入；日志来源类名为 WebRequestAuditLogRepository）。
        /// </summary>
        private static readonly ILogger NLogLogger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 创建 WebRequestAuditLogRepository。
        /// </summary>
        /// <param name="contextFactory">DbContext 工厂。</param>
        public WebRequestAuditLogRepository(IDbContextFactory<SortingHubDbContext> contextFactory)
            : base(contextFactory, NLogLogger) {
        }

        /// <summary>
        /// 新增 Web 请求审计日志聚合，并在同一事务中落库冷表详情。
        /// </summary>
        /// <param name="auditLog">审计日志聚合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>仓储执行结果。</returns>
        public override async Task<RepositoryResult> AddAsync(WebRequestAuditLog auditLog, CancellationToken cancellationToken) {
            if (auditLog is null) {
                return RepositoryResult.Fail("审计日志不能为空");
            }

            if (auditLog.Detail is not null && auditLog.Detail.StartedAt == default) {
                auditLog.Detail.StartedAt = auditLog.StartedAt;
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                await db.Set<WebRequestAuditLog>().AddAsync(auditLog, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return RepositoryResult.Success();
            }
            catch (OperationCanceledException ex) {
                NLogLogger.Warn(ex, "写入 Web 请求审计日志被取消，TraceId={TraceId}, CorrelationId={CorrelationId}", auditLog.TraceId, auditLog.CorrelationId);
                return RepositoryResult.Fail("操作已取消");
            }
            catch (Exception ex) {
                NLogLogger.Error(ex, "写入 Web 请求审计日志失败，TraceId={TraceId}, CorrelationId={CorrelationId}", auditLog.TraceId, auditLog.CorrelationId);
                return RepositoryResult.Fail("写入 Web 请求审计日志失败");
            }
        }

        /// <summary>
        /// 按过滤条件分页查询审计日志摘要。
        /// </summary>
        /// <param name="filter">过滤条件。</param>
        /// <param name="pageRequest">分页参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>分页摘要结果。</returns>
        public async Task<PageResult<WebRequestAuditLogSummaryReadModel>> GetPagedAsync(
            WebRequestAuditLogQueryFilter filter,
            PageRequest pageRequest,
            CancellationToken cancellationToken) {
            if (filter is null) {
                throw new ArgumentNullException(nameof(filter));
            }

            if (pageRequest is null) {
                throw new ArgumentNullException(nameof(pageRequest));
            }

            var pageNumber = pageRequest.NormalizePageNumber();
            var pageSize = pageRequest.NormalizePageSize();

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                var query = ApplyFilter(db.Set<WebRequestAuditLog>().AsNoTracking(), filter);
                var totalCount = await query.LongCountAsync(cancellationToken);
                var items = await query
                    .OrderByDescending(x => x.StartedAt)
                    .ThenByDescending(x => x.Id)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new WebRequestAuditLogSummaryReadModel {
                        Id = x.Id,
                        TraceId = x.TraceId,
                        CorrelationId = x.CorrelationId,
                        RequestMethod = x.RequestMethod,
                        RequestPath = x.RequestPath,
                        StatusCode = x.StatusCode,
                        IsSuccess = x.IsSuccess,
                        StartedAt = x.StartedAt,
                        DurationMs = x.DurationMs
                    })
                    .ToListAsync(cancellationToken);

                return new PageResult<WebRequestAuditLogSummaryReadModel> {
                    Items = items,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }
            catch (Exception exception) {
                NLogLogger.Error(
                    exception,
                    "分页查询 Web 请求审计日志失败，PageNumber={PageNumber}, PageSize={PageSize}, StartedAtStart={StartedAtStart}, StartedAtEnd={StartedAtEnd}, StatusCode={StatusCode}, IsSuccess={IsSuccess}, TraceId={TraceId}, CorrelationId={CorrelationId}, RequestPathKeyword={RequestPathKeyword}",
                    pageNumber,
                    pageSize,
                    filter.StartedAtStart,
                    filter.StartedAtEnd,
                    filter.StatusCode,
                    filter.IsSuccess,
                    filter.TraceId,
                    filter.CorrelationId,
                    filter.RequestPathKeyword);
                throw;
            }
        }

        /// <summary>
        /// 按主键查询审计日志详情（包含冷表详情）。
        /// </summary>
        /// <param name="id">主键。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>详情读模型，不存在时返回 null。</returns>
        public async Task<WebRequestAuditLogDetailReadModel?> GetByIdAsync(long id, CancellationToken cancellationToken) {
            if (id <= 0) {
                return null;
            }

            try {
                await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
                var entity = await db.Set<WebRequestAuditLog>()
                    .AsNoTracking()
                    .Include(x => x.Detail)
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (entity is null) {
                    return null;
                }

                var detail = entity.Detail;
                return new WebRequestAuditLogDetailReadModel {
                    Id = entity.Id,
                    TraceId = entity.TraceId,
                    CorrelationId = entity.CorrelationId,
                    SpanId = entity.SpanId,
                    OperationName = entity.OperationName,
                    RequestMethod = entity.RequestMethod,
                    RequestScheme = entity.RequestScheme,
                    RequestHost = entity.RequestHost,
                    RequestPort = entity.RequestPort,
                    RequestPath = entity.RequestPath,
                    RequestRouteTemplate = entity.RequestRouteTemplate,
                    UserId = entity.UserId,
                    UserName = entity.UserName,
                    IsAuthenticated = entity.IsAuthenticated,
                    TenantId = entity.TenantId,
                    RequestPayloadType = entity.RequestPayloadType,
                    RequestSizeBytes = entity.RequestSizeBytes,
                    HasRequestBody = entity.HasRequestBody,
                    IsRequestBodyTruncated = entity.IsRequestBodyTruncated,
                    ResponsePayloadType = entity.ResponsePayloadType,
                    ResponseSizeBytes = entity.ResponseSizeBytes,
                    HasResponseBody = entity.HasResponseBody,
                    IsResponseBodyTruncated = entity.IsResponseBodyTruncated,
                    StatusCode = entity.StatusCode,
                    IsSuccess = entity.IsSuccess,
                    HasException = entity.HasException,
                    AuditResourceType = entity.AuditResourceType,
                    ResourceId = entity.ResourceId,
                    StartedAt = entity.StartedAt,
                    EndedAt = entity.EndedAt,
                    DurationMs = entity.DurationMs,
                    CreatedAt = entity.CreatedAt,
                    RequestUrl = detail?.RequestUrl ?? string.Empty,
                    RequestQueryString = detail?.RequestQueryString ?? string.Empty,
                    RequestHeadersJson = detail?.RequestHeadersJson ?? string.Empty,
                    ResponseHeadersJson = detail?.ResponseHeadersJson ?? string.Empty,
                    RequestContentType = detail?.RequestContentType ?? string.Empty,
                    ResponseContentType = detail?.ResponseContentType ?? string.Empty,
                    Accept = detail?.Accept ?? string.Empty,
                    Referer = detail?.Referer ?? string.Empty,
                    Origin = detail?.Origin ?? string.Empty,
                    AuthorizationType = detail?.AuthorizationType ?? string.Empty,
                    UserAgent = detail?.UserAgent ?? string.Empty,
                    RequestBody = detail?.RequestBody ?? string.Empty,
                    ResponseBody = detail?.ResponseBody ?? string.Empty,
                    CurlCommand = detail?.CurlCommand ?? string.Empty,
                    ErrorMessage = detail?.ErrorMessage ?? string.Empty,
                    ExceptionType = detail?.ExceptionType ?? string.Empty,
                    ErrorCode = detail?.ErrorCode ?? string.Empty,
                    ExceptionStackTrace = detail?.ExceptionStackTrace ?? string.Empty,
                    FileMetadataJson = detail?.FileMetadataJson ?? string.Empty,
                    HasFileAccess = detail?.HasFileAccess ?? false,
                    FileOperationType = detail?.FileOperationType ?? Domain.Enums.AuditLogs.FileOperationType.None,
                    FileCount = detail?.FileCount ?? 0,
                    FileTotalBytes = detail?.FileTotalBytes ?? 0L,
                    ImageMetadataJson = detail?.ImageMetadataJson ?? string.Empty,
                    HasImageAccess = detail?.HasImageAccess ?? false,
                    ImageCount = detail?.ImageCount ?? 0,
                    DatabaseOperationSummary = detail?.DatabaseOperationSummary ?? string.Empty,
                    HasDatabaseAccess = detail?.HasDatabaseAccess ?? false,
                    DatabaseAccessCount = detail?.DatabaseAccessCount ?? 0,
                    DatabaseDurationMs = detail?.DatabaseDurationMs ?? 0L,
                    ResourceCode = detail?.ResourceCode ?? string.Empty,
                    ResourceName = detail?.ResourceName ?? string.Empty,
                    ActionDurationMs = detail?.ActionDurationMs ?? 0L,
                    MiddlewareDurationMs = detail?.MiddlewareDurationMs ?? 0L,
                    Tags = detail?.Tags ?? string.Empty,
                    ExtraPropertiesJson = detail?.ExtraPropertiesJson ?? string.Empty,
                    Remark = detail?.Remark ?? string.Empty
                };
            }
            catch (Exception exception) {
                NLogLogger.Error(exception, "按 Id 查询 Web 请求审计日志详情失败，Id={AuditLogId}", id);
                throw;
            }
        }

        /// <summary>
        /// 应用审计日志查询过滤条件。
        /// </summary>
        /// <param name="query">基础查询。</param>
        /// <param name="filter">过滤参数。</param>
        /// <returns>应用过滤后的查询。</returns>
        private static IQueryable<WebRequestAuditLog> ApplyFilter(
            IQueryable<WebRequestAuditLog> query,
            WebRequestAuditLogQueryFilter filter) {
            if (filter.StartedAtStart.HasValue) {
                query = query.Where(x => x.StartedAt >= filter.StartedAtStart.Value);
            }

            if (filter.StartedAtEnd.HasValue) {
                query = query.Where(x => x.StartedAt <= filter.StartedAtEnd.Value);
            }

            if (filter.StatusCode.HasValue) {
                query = query.Where(x => x.StatusCode == filter.StatusCode.Value);
            }

            if (filter.IsSuccess.HasValue) {
                query = query.Where(x => x.IsSuccess == filter.IsSuccess.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.TraceId)) {
                var traceId = filter.TraceId.Trim();
                query = query.Where(x => x.TraceId == traceId);
            }

            if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) {
                var correlationId = filter.CorrelationId.Trim();
                query = query.Where(x => x.CorrelationId == correlationId);
            }

            if (!string.IsNullOrWhiteSpace(filter.RequestPathKeyword)) {
                var requestPathKeyword = filter.RequestPathKeyword.Trim();
                query = query.Where(x => x.RequestPath.Contains(requestPathKeyword));
            }

            return query;
        }
    }
}
