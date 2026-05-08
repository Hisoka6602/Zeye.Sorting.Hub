using Microsoft.AspNetCore.Mvc;
using NLog;
using Zeye.Sorting.Hub.Application.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>
/// 业务模块路由约定扩展。
/// </summary>
public static class EndpointRouteBuilderConventionExtensions {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 创建带统一标签的业务模块路由组。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <param name="routePrefix">路由前缀。</param>
    /// <param name="moduleTag">模块标签。</param>
    /// <returns>路由组构建器。</returns>
    public static RouteGroupBuilder MapBusinessModuleGroup(this IEndpointRouteBuilder routeBuilder, string routePrefix, string moduleTag) {
        ValidateRouteBuilder(routeBuilder);
        ValidateRoutePrefix(routePrefix);
        ValidateRequiredText(moduleTag, nameof(moduleTag), "创建业务模块路由组");
        return routeBuilder.MapGroup(routePrefix).WithTags(moduleTag.Trim());
    }

    /// <summary>
    /// 为业务模块端点应用统一命名、说明与错误响应约定。
    /// </summary>
    /// <param name="routeBuilder">路由处理器构建器。</param>
    /// <param name="endpointName">端点名称。</param>
    /// <param name="summary">摘要。</param>
    /// <param name="description">说明。</param>
    /// <param name="problemStatusCodes">需要声明的 ProblemDetails 状态码。</param>
    /// <returns>路由处理器构建器。</returns>
    public static RouteHandlerBuilder WithBusinessModuleEndpointConvention(
        this RouteHandlerBuilder routeBuilder,
        string endpointName,
        string summary,
        string description,
        params int[]? problemStatusCodes) {
        ValidateRouteHandlerBuilder(routeBuilder);
        ValidateRequiredText(endpointName, nameof(endpointName), "应用业务模块端点约定");
        ValidateRequiredText(summary, nameof(summary), "应用业务模块端点约定");
        ValidateRequiredText(description, nameof(description), "应用业务模块端点约定");

        var builder = routeBuilder
            .WithName(endpointName.Trim())
            .WithSummary(summary.Trim())
            .WithDescription(description.Trim());

        var normalizedProblemStatusCodes = NormalizeProblemStatusCodes(problemStatusCodes);
        foreach (var problemStatusCode in normalizedProblemStatusCodes) {
            if (problemStatusCode is < ApplicationResult.BadRequestStatusCode or > 599) {
                Logger.Warn("应用业务模块端点约定时发现非法 ProblemDetails 状态码，StatusCode={StatusCode}", problemStatusCode);
                throw new ArgumentOutOfRangeException(nameof(problemStatusCodes), "ProblemDetails 状态码必须位于 400-599 范围内。");
            }

            builder.ProducesProblem(problemStatusCode);
        }

        return builder;
    }

    /// <summary>
    /// 将应用层失败结果映射为统一 ProblemDetails 响应。
    /// </summary>
    /// <param name="result">应用层结果。</param>
    /// <returns>ProblemDetails 响应。</returns>
    public static IResult ToProblemResult(this ApplicationResult result) {
        if (result is null) {
            Logger.Warn("尝试将空的 ApplicationResult 映射为 ProblemDetails。");
            throw new ArgumentNullException(nameof(result));
        }

        if (result.IsSuccess) {
            Logger.Warn("尝试将成功的 ApplicationResult 映射为 ProblemDetails。StatusCode={StatusCode}", result.StatusCode);
            throw new InvalidOperationException("成功结果不能映射为 ProblemDetails。");
        }

        var normalizedStatusCode = NormalizeProblemStatusCode(result.StatusCode);
        var problemDetails = new ProblemDetails {
            Title = string.IsNullOrWhiteSpace(result.ProblemTitle)
                ? ApplicationResult.DefaultProblemTitle
                : result.ProblemTitle,
            Detail = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? ApplicationResult.DefaultErrorMessage
                : result.ErrorMessage,
            Status = normalizedStatusCode
        };
        if (!string.IsNullOrWhiteSpace(result.ErrorCode)) {
            problemDetails.Extensions["errorCode"] = result.ErrorCode;
        }

        return Results.Json(
            problemDetails,
            contentType: "application/problem+json",
            statusCode: normalizedStatusCode);
    }

    /// <summary>
    /// 归一化 ProblemDetails 状态码声明列表。
    /// </summary>
    /// <param name="problemStatusCodes">原始状态码声明列表。</param>
    /// <returns>去重后的状态码集合。</returns>
    private static IEnumerable<int> NormalizeProblemStatusCodes(int[]? problemStatusCodes) {
        if (problemStatusCodes is null) {
            Logger.Warn("应用业务模块端点约定时 ProblemDetails 状态码声明为空数组引用，已按空集合处理。");
            return [];
        }

        return problemStatusCodes.Distinct();
    }

    /// <summary>
    /// 归一化 ProblemDetails 响应状态码。
    /// </summary>
    /// <param name="statusCode">原始状态码。</param>
    /// <returns>可安全返回的响应状态码。</returns>
    private static int NormalizeProblemStatusCode(int statusCode) {
        if (statusCode is >= ApplicationResult.BadRequestStatusCode and <= 599) {
            return statusCode;
        }

        Logger.Warn("应用层结果状态码非法，已降级为 500。StatusCode={StatusCode}", statusCode);
        return ApplicationResult.InternalServerErrorStatusCode;
    }

    /// <summary>
    /// 校验路由构建器。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    private static void ValidateRouteBuilder(IEndpointRouteBuilder routeBuilder) {
        if (routeBuilder is null) {
            Logger.Warn("创建业务模块路由组时路由构建器为空。");
            throw new ArgumentNullException(nameof(routeBuilder));
        }
    }

    /// <summary>
    /// 校验路由处理器构建器。
    /// </summary>
    /// <param name="routeBuilder">路由处理器构建器。</param>
    private static void ValidateRouteHandlerBuilder(RouteHandlerBuilder routeBuilder) {
        if (routeBuilder is null) {
            Logger.Warn("应用业务模块端点约定时路由处理器构建器为空。");
            throw new ArgumentNullException(nameof(routeBuilder));
        }
    }

    /// <summary>
    /// 校验路由前缀。
    /// </summary>
    /// <param name="routePrefix">路由前缀。</param>
    private static void ValidateRoutePrefix(string routePrefix) {
        if (string.IsNullOrWhiteSpace(routePrefix)) {
            Logger.Warn("创建业务模块路由组时路由前缀为空。");
            throw new ArgumentException("routePrefix 不能为空。", nameof(routePrefix));
        }

        if (!routePrefix.StartsWith("/", StringComparison.Ordinal)) {
            Logger.Warn("创建业务模块路由组时路由前缀非法，RoutePrefix={RoutePrefix}", routePrefix);
            throw new ArgumentException("routePrefix 必须以 / 开头。", nameof(routePrefix));
        }
    }

    /// <summary>
    /// 校验文本参数。
    /// </summary>
    /// <param name="value">文本值。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="logContext">日志上下文。</param>
    private static void ValidateRequiredText(string value, string paramName, string logContext) {
        if (string.IsNullOrWhiteSpace(value)) {
            Logger.Warn("{LogContext}时文本参数为空，ParamName={ParamName}", logContext, paramName);
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }
    }
}
