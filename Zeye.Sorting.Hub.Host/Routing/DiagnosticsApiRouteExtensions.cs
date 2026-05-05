using NLog;
using Zeye.Sorting.Hub.Application.Services.Diagnostics;
using Zeye.Sorting.Hub.Contracts.Models.Diagnostics;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>
/// 诊断 API 路由扩展。
/// </summary>
public static class DiagnosticsApiRouteExtensions {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册慢查询画像诊断 API。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapDiagnosticsApis(this IEndpointRouteBuilder routeBuilder) {
        var group = routeBuilder.MapGroup("/api/diagnostics").WithTags("Diagnostics");

        group.MapGet("/slow-queries", GetSlowQueryProfiles)
            .WithName("GetSlowQueryProfiles")
            .WithSummary("获取慢查询画像列表")
            .WithDescription("返回当前内存窗口内的慢查询指纹聚合快照；只读取进程内快照，不触发数据库重查询。")
            .Produces<SlowQueryProfileListResponse>(StatusCodes.Status200OK);

        group.MapGet("/slow-queries/{fingerprint}", GetSlowQueryProfileByFingerprint)
            .WithName("GetSlowQueryProfileByFingerprint")
            .WithSummary("获取指定慢查询画像详情")
            .WithDescription("按慢查询指纹读取当前窗口内的画像详情；仅查询内存快照，不访问数据库。")
            .Produces<SlowQueryProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routeBuilder;
    }

    /// <summary>
    /// 处理慢查询画像列表请求。
    /// </summary>
    /// <param name="queryService">查询服务。</param>
    /// <returns>画像列表。</returns>
    private static IResult GetSlowQueryProfiles(GetSlowQueryProfileQueryService queryService) {
        var response = queryService.Execute();
        return Results.Ok(response);
    }

    /// <summary>
    /// 处理慢查询画像详情请求。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="queryService">查询服务。</param>
    /// <returns>画像详情。</returns>
    private static IResult GetSlowQueryProfileByFingerprint(
        string fingerprint,
        GetSlowQueryProfileQueryService queryService) {
        try {
            var response = queryService.Execute(fingerprint);
            return response is null
                ? LocalDateTimeParsing.CreateNotFoundProblem("慢查询画像不存在", $"未找到指纹为 {fingerprint} 的慢查询画像。")
                : Results.Ok(response);
        }
        catch (ArgumentException exception) {
            NLogLogger.Warn(exception, "慢查询画像详情参数校验失败。Fingerprint={Fingerprint}", fingerprint);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }
}
