using NLog;
using Zeye.Sorting.Hub.Application.Services.DataGovernance;
using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>
/// 数据治理 API 路由扩展。
/// </summary>
public static class DataGovernanceApiRouteExtensions {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册数据治理 API。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapDataGovernanceApis(this IEndpointRouteBuilder routeBuilder) {
        var group = routeBuilder.MapGroup("/api/data-governance/archive-tasks").WithTags("DataGovernance");

        group.MapPost(string.Empty, CreateArchiveTaskAsync)
            .WithName("CreateArchiveTask")
            .WithSummary("创建归档 dry-run 任务")
            .WithDescription("创建数据归档 dry-run 任务；当前仅支持 WebRequestAuditLogHistory，系统只生成计划与审计摘要，不执行真实删除或迁移。")
            .Produces<ArchiveTaskResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet(string.Empty, GetArchiveTaskListAsync)
            .WithName("GetArchiveTaskList")
            .WithSummary("分页查询归档任务")
            .WithDescription("按页码、页大小、状态与任务类型分页查询归档任务列表；仅返回任务状态与 dry-run 计划摘要，不触发数据库重跑。")
            .Produces<ArchiveTaskListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:long}/retry", RetryArchiveTaskAsync)
            .WithName("RetryArchiveTask")
            .WithSummary("重试终态归档任务")
            .WithDescription("将已完成或已失败的 dry-run 归档任务重新放回待执行队列；执行中的任务不允许重试。")
            .Produces<ArchiveTaskResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routeBuilder;
    }

    /// <summary>
    /// 处理创建归档任务请求。
    /// </summary>
    /// <param name="request">创建请求。</param>
    /// <param name="commandService">创建服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>创建结果。</returns>
    private static async Task<IResult> CreateArchiveTaskAsync(
        ArchiveTaskCreateRequest request,
        CreateArchiveTaskCommandService commandService,
        CancellationToken cancellationToken) {
        if (request is null) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "请求体不能为空。");
        }

        try {
            var response = await commandService.ExecuteAsync(request, cancellationToken);
            return Results.Created($"/api/data-governance/archive-tasks/{response.Id}", response);
        }
        catch (ArgumentException exception) {
            NLogLogger.Warn(exception, "创建归档任务参数校验失败。TaskType={TaskType}", request.TaskType);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理归档任务分页查询。
    /// </summary>
    /// <param name="query">查询参数。</param>
    /// <param name="queryService">查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>分页结果。</returns>
    private static async Task<IResult> GetArchiveTaskListAsync(
        [AsParameters] ArchiveTaskListRequest query,
        GetArchiveTaskPagedQueryService queryService,
        CancellationToken cancellationToken) {
        try {
            var response = await queryService.ExecuteAsync(query, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            NLogLogger.Warn(exception, "归档任务分页查询参数校验失败。Status={Status}, TaskType={TaskType}", query.Status, query.TaskType);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理归档任务重试请求。
    /// </summary>
    /// <param name="id">任务主键。</param>
    /// <param name="commandService">重试服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>重试结果。</returns>
    private static async Task<IResult> RetryArchiveTaskAsync(
        long id,
        RetryArchiveTaskCommandService commandService,
        CancellationToken cancellationToken) {
        try {
            var response = await commandService.ExecuteAsync(id, cancellationToken);
            return response is null
                ? LocalDateTimeParsing.CreateNotFoundProblem("归档任务不存在", $"未找到 Id 为 {id} 的归档任务。")
                : Results.Ok(response);
        }
        catch (ArgumentOutOfRangeException exception) {
            NLogLogger.Warn(exception, "重试归档任务范围校验失败。TaskId={TaskId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
        catch (InvalidOperationException exception) {
            NLogLogger.Warn(exception, "重试归档任务状态校验失败。TaskId={TaskId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }
}
