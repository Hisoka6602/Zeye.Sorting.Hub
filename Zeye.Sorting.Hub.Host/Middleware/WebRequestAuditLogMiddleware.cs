using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;

namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计日志中间件。
/// </summary>
public sealed class WebRequestAuditLogMiddleware {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 下一个中间件委托。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 审计配置。
    /// </summary>
    private readonly WebRequestAuditLogOptions _options;

    /// <summary>
    /// JSON 序列化选项。
    /// </summary>
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() {
        WriteIndented = false
    };

    /// <summary>
    /// 创建中间件实例。
    /// </summary>
    /// <param name="next">下一个中间件委托。</param>
    /// <param name="options">审计配置。</param>
    public WebRequestAuditLogMiddleware(
        RequestDelegate next,
        IOptions<WebRequestAuditLogOptions> options) {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 执行中间件。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="writeService">审计写入应用服务。</param>
    /// <returns>异步任务。</returns>
    public async Task InvokeAsync(HttpContext context, WriteWebRequestAuditLogCommandService writeService) {
        if (!_options.Enabled || !ShouldSample(_options.SampleRate)) {
            await _next(context);
            return;
        }

        // 步骤 1：初始化请求期采集上下文。
        var startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();
        var traceId = ResolveTraceId(context);
        var correlationId = ResolveCorrelationId(context, traceId);
        var routeTemplate = string.Empty;
        var requestBodyCapture = CapturedBody.Empty;
        var responseBodyCapture = CapturedBody.Empty;
        var capturedException = (Exception?)null;
        var requestSizeBytes = context.Request.ContentLength ?? 0L;

        if (_options.IncludeRequestBody) {
            requestBodyCapture = await CaptureRequestBodyAsync(context.Request, _options.MaxRequestBodyLength);
            requestSizeBytes = requestBodyCapture.OriginalLengthBytes;
        }

        var originalResponseBody = context.Response.Body;
        var responseCaptureStream = _options.IncludeResponseBody ? new MemoryStream() : null;
        if (responseCaptureStream is not null) {
            context.Response.Body = responseCaptureStream;
        }

        // 步骤 2：注册响应完成回调，确保获取最终状态码并落审计。
        context.Response.OnCompleted(async () => {
            try {
                routeTemplate = ResolveRouteTemplate(context, routeTemplate);
                var endedAt = DateTime.Now;
                var durationMs = stopwatch.ElapsedMilliseconds;
                var statusCode = context.Response.StatusCode;
                var isSuccess = statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status400BadRequest;
                var detail = BuildDetail(
                    context,
                    startedAt,
                    requestBodyCapture,
                    responseBodyCapture,
                    capturedException,
                    traceId,
                    correlationId);
                var log = new WebRequestAuditLog {
                    TraceId = traceId,
                    CorrelationId = correlationId,
                    SpanId = ResolveSpanId(),
                    OperationName = ResolveOperationName(context, routeTemplate),
                    RequestMethod = context.Request.Method,
                    RequestScheme = context.Request.Scheme,
                    RequestHost = context.Request.Host.Host,
                    RequestPort = context.Request.Host.Port,
                    RequestPath = context.Request.Path.Value ?? string.Empty,
                    RequestRouteTemplate = routeTemplate,
                    UserName = context.User.Identity?.Name ?? string.Empty,
                    IsAuthenticated = context.User.Identity?.IsAuthenticated ?? false,
                    RequestPayloadType = ResolveRequestPayloadType(context.Request.ContentType, requestBodyCapture.HasBody),
                    RequestSizeBytes = Math.Max(0L, requestSizeBytes),
                    HasRequestBody = requestBodyCapture.HasBody,
                    IsRequestBodyTruncated = requestBodyCapture.IsTruncated,
                    ResponsePayloadType = ResolveResponsePayloadType(context.Response.ContentType, responseBodyCapture.HasBody),
                    ResponseSizeBytes = Math.Max(0L, responseBodyCapture.OriginalLengthBytes),
                    HasResponseBody = responseBodyCapture.HasBody,
                    IsResponseBodyTruncated = responseBodyCapture.IsTruncated,
                    StatusCode = statusCode,
                    IsSuccess = isSuccess,
                    HasException = capturedException is not null,
                    AuditResourceType = AuditResourceType.Api,
                    ResourceId = context.Request.Path.Value ?? string.Empty,
                    StartedAt = startedAt,
                    EndedAt = endedAt,
                    DurationMs = Math.Max(0L, durationMs),
                    CreatedAt = endedAt,
                    Detail = detail
                };

                var result = await writeService.WriteAsync(log, context.RequestAborted);
                if (!result.IsSuccess) {
                    NLogLogger.Error("写入 Web 请求审计日志返回失败，TraceId={TraceId}, CorrelationId={CorrelationId}, ErrorCode={ErrorCode}, ErrorMessage={ErrorMessage}",
                        traceId,
                        correlationId,
                        result.ErrorCode,
                        result.ErrorMessage);
                }
            }
            catch (Exception ex) {
                NLogLogger.Error(ex, "写入 Web 请求审计日志发生异常，TraceId={TraceId}, CorrelationId={CorrelationId}", traceId, correlationId);
            }
        });

        try {
            // 步骤 3：继续执行后续管线并记录异常信息。
            await _next(context);
            routeTemplate = ResolveRouteTemplate(context, routeTemplate);
        }
        catch (Exception ex) {
            capturedException = ex;
            routeTemplate = ResolveRouteTemplate(context, routeTemplate);
            throw;
        }
        finally {
            // 步骤 4：恢复响应流并完成响应体采集（失败不得污染主请求）。
            if (responseCaptureStream is not null) {
                try {
                    responseBodyCapture = await CaptureResponseBodyAsync(responseCaptureStream, _options.MaxResponseBodyLength);
                    responseCaptureStream.Position = 0;
                    await responseCaptureStream.CopyToAsync(originalResponseBody);
                    await originalResponseBody.FlushAsync();
                }
                finally {
                    context.Response.Body = originalResponseBody;
                    await responseCaptureStream.DisposeAsync();
                }
            }
        }
    }

    /// <summary>
    /// 生成详情实体。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="startedAt">开始时间。</param>
    /// <param name="requestBodyCapture">请求体采集结果。</param>
    /// <param name="responseBodyCapture">响应体采集结果。</param>
    /// <param name="capturedException">已捕获异常。</param>
    /// <param name="traceId">追踪 Id。</param>
    /// <param name="correlationId">关联 Id。</param>
    /// <returns>详情实体。</returns>
    private static WebRequestAuditLogDetail BuildDetail(
        HttpContext context,
        DateTime startedAt,
        CapturedBody requestBodyCapture,
        CapturedBody responseBodyCapture,
        Exception? capturedException,
        string traceId,
        string correlationId) {
        var requestUrl = BuildRequestUrl(context.Request);
        var requestHeaders = SerializeHeaders(context.Request.Headers);
        var responseHeaders = SerializeHeaders(context.Response.Headers);
        var errorMessage = capturedException?.Message ?? string.Empty;
        var exceptionType = capturedException?.GetType().FullName ?? string.Empty;
        var exceptionStackTrace = capturedException?.ToString() ?? string.Empty;

        return new WebRequestAuditLogDetail {
            StartedAt = startedAt,
            RequestUrl = requestUrl,
            RequestQueryString = context.Request.QueryString.Value ?? string.Empty,
            RequestHeadersJson = requestHeaders,
            ResponseHeadersJson = responseHeaders,
            RequestContentType = context.Request.ContentType ?? string.Empty,
            ResponseContentType = context.Response.ContentType ?? string.Empty,
            Accept = context.Request.Headers.Accept.ToString(),
            Referer = context.Request.Headers.Referer.ToString(),
            Origin = context.Request.Headers.Origin.ToString(),
            AuthorizationType = ResolveAuthorizationType(context.Request.Headers.Authorization.ToString()),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            RequestBody = requestBodyCapture.Content,
            ResponseBody = responseBodyCapture.Content,
            CurlCommand = BuildCurlCommand(context.Request, requestBodyCapture.Content),
            ErrorMessage = errorMessage,
            ExceptionType = exceptionType,
            ExceptionStackTrace = exceptionStackTrace,
            ExtraPropertiesJson = BuildExtraPropertiesJson(traceId, correlationId),
            DatabaseOperationSummary = string.Empty,
            Tags = "web-request-audit"
        };
    }

    /// <summary>
    /// 采集请求体。
    /// </summary>
    /// <param name="request">请求对象。</param>
    /// <param name="maxLength">最大采集长度。</param>
    /// <returns>采集结果。</returns>
    private static async Task<CapturedBody> CaptureRequestBodyAsync(HttpRequest request, int maxLength) {
        if (maxLength == 0 || request.Body == Stream.Null || !request.Body.CanRead) {
            return CapturedBody.Empty;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        var content = await ReadBodyWithinLimitAsync(request.Body, maxLength);
        request.Body.Position = 0;
        return content;
    }

    /// <summary>
    /// 采集响应体。
    /// </summary>
    /// <param name="responseCaptureStream">响应采集流。</param>
    /// <param name="maxLength">最大采集长度。</param>
    /// <returns>采集结果。</returns>
    private static async Task<CapturedBody> CaptureResponseBodyAsync(MemoryStream responseCaptureStream, int maxLength) {
        if (maxLength == 0) {
            return new CapturedBody(string.Empty, false, false, responseCaptureStream.Length);
        }

        responseCaptureStream.Position = 0;
        var content = await ReadBodyWithinLimitAsync(responseCaptureStream, maxLength);
        return new CapturedBody(content.Content, responseCaptureStream.Length > 0, content.IsTruncated, responseCaptureStream.Length);
    }

    /// <summary>
    /// 在长度上限内读取正文。
    /// </summary>
    /// <param name="stream">正文流。</param>
    /// <param name="maxLength">最大采集长度。</param>
    /// <returns>采集结果。</returns>
    private static async Task<CapturedBody> ReadBodyWithinLimitAsync(Stream stream, int maxLength) {
        if (maxLength <= 0 || !stream.CanRead) {
            return CapturedBody.Empty;
        }

        var buffer = new char[maxLength + 1];
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var readCount = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        var contentLength = Math.Min(readCount, maxLength);
        var content = new string(buffer, 0, contentLength);
        var isTruncated = readCount > maxLength;
        var bodyBytes = Encoding.UTF8.GetByteCount(content);
        return new CapturedBody(content, readCount > 0, isTruncated, bodyBytes);
    }

    /// <summary>
    /// 解析请求路由模板。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>路由模板。</returns>
    private static string ResolveRouteTemplate(HttpContext context, string fallback) {
        if (context.GetEndpoint() is RouteEndpoint routeEndpoint) {
            return routeEndpoint.RoutePattern.RawText ?? routeEndpoint.DisplayName ?? fallback;
        }

        return fallback;
    }

    /// <summary>
    /// 解析追踪 Id。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <returns>追踪 Id。</returns>
    private static string ResolveTraceId(HttpContext context) {
        var activityTraceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(activityTraceId)) {
            return activityTraceId;
        }

        return context.TraceIdentifier;
    }

    /// <summary>
    /// 解析 SpanId。
    /// </summary>
    /// <returns>SpanId。</returns>
    private static string ResolveSpanId() {
        var spanId = Activity.Current?.SpanId.ToString();
        return spanId ?? string.Empty;
    }

    /// <summary>
    /// 解析 CorrelationId。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>关联 Id。</returns>
    private static string ResolveCorrelationId(HttpContext context, string fallback) {
        var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
        return string.IsNullOrWhiteSpace(correlationId) ? fallback : correlationId;
    }

    /// <summary>
    /// 判断是否命中采样。
    /// </summary>
    /// <param name="sampleRate">采样率。</param>
    /// <returns>是否采样。</returns>
    private static bool ShouldSample(double sampleRate) {
        if (sampleRate <= 0D) {
            return false;
        }

        if (sampleRate >= 1D) {
            return true;
        }

        return Random.Shared.NextDouble() <= sampleRate;
    }

    /// <summary>
    /// 解析请求载荷类型。
    /// </summary>
    /// <param name="contentType">Content-Type。</param>
    /// <param name="hasBody">是否存在正文。</param>
    /// <returns>请求载荷类型。</returns>
    private static WebRequestPayloadType ResolveRequestPayloadType(string? contentType, bool hasBody) {
        if (!hasBody) {
            return WebRequestPayloadType.None;
        }

        if (string.IsNullOrWhiteSpace(contentType)) {
            return WebRequestPayloadType.Unknown;
        }

        var normalized = contentType.ToLowerInvariant();
        if (normalized.Contains("application/json", StringComparison.Ordinal)) {
            return WebRequestPayloadType.Json;
        }

        if (normalized.Contains("multipart/form-data", StringComparison.Ordinal)) {
            return WebRequestPayloadType.MultipartFormData;
        }

        if (normalized.Contains("application/x-www-form-urlencoded", StringComparison.Ordinal)) {
            return WebRequestPayloadType.Form;
        }

        if (normalized.StartsWith("text/", StringComparison.Ordinal)) {
            return WebRequestPayloadType.Text;
        }

        return WebRequestPayloadType.Binary;
    }

    /// <summary>
    /// 解析响应载荷类型。
    /// </summary>
    /// <param name="contentType">Content-Type。</param>
    /// <param name="hasBody">是否存在正文。</param>
    /// <returns>响应载荷类型。</returns>
    private static WebResponsePayloadType ResolveResponsePayloadType(string? contentType, bool hasBody) {
        if (!hasBody) {
            return WebResponsePayloadType.None;
        }

        if (string.IsNullOrWhiteSpace(contentType)) {
            return WebResponsePayloadType.Unknown;
        }

        var normalized = contentType.ToLowerInvariant();
        if (normalized.Contains("application/json", StringComparison.Ordinal)) {
            return WebResponsePayloadType.Json;
        }

        if (normalized.Contains("text/html", StringComparison.Ordinal)) {
            return WebResponsePayloadType.Html;
        }

        if (normalized.StartsWith("text/", StringComparison.Ordinal)) {
            return WebResponsePayloadType.Text;
        }

        return WebResponsePayloadType.Binary;
    }

    /// <summary>
    /// 构建操作名称。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="routeTemplate">路由模板。</param>
    /// <returns>操作名称。</returns>
    private static string ResolveOperationName(HttpContext context, string routeTemplate) {
        var endpointName = context.GetEndpoint()?.DisplayName;
        if (!string.IsNullOrWhiteSpace(endpointName)) {
            return endpointName;
        }

        if (!string.IsNullOrWhiteSpace(routeTemplate)) {
            return routeTemplate;
        }

        return $"{context.Request.Method} {context.Request.Path}";
    }

    /// <summary>
    /// 构建完整请求地址。
    /// </summary>
    /// <param name="request">请求对象。</param>
    /// <returns>完整地址。</returns>
    private static string BuildRequestUrl(HttpRequest request) {
        return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
    }

    /// <summary>
    /// 序列化 Header 集合。
    /// </summary>
    /// <param name="headers">Header 集合。</param>
    /// <returns>JSON 文本。</returns>
    private static string SerializeHeaders(IHeaderDictionary headers) {
        var values = headers.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToString());
        return JsonSerializer.Serialize(values, JsonSerializerOptions);
    }

    /// <summary>
    /// 解析授权类型。
    /// </summary>
    /// <param name="authorizationHeader">授权头。</param>
    /// <returns>授权类型。</returns>
    private static string ResolveAuthorizationType(string authorizationHeader) {
        if (string.IsNullOrWhiteSpace(authorizationHeader)) {
            return string.Empty;
        }

        var parts = authorizationHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    /// <summary>
    /// 构建 Curl 回放命令。
    /// </summary>
    /// <param name="request">请求对象。</param>
    /// <param name="requestBody">请求体文本。</param>
    /// <returns>Curl 命令。</returns>
    private static string BuildCurlCommand(HttpRequest request, string requestBody) {
        var builder = new StringBuilder();
        builder.Append("curl -X ").Append(request.Method).Append(' ');
        builder.Append('"').Append(BuildRequestUrl(request)).Append('"');

        if (!string.IsNullOrWhiteSpace(request.ContentType)) {
            builder.Append(" -H \"Content-Type: ").Append(request.ContentType).Append("\"");
        }

        if (!string.IsNullOrWhiteSpace(requestBody)) {
            builder.Append(" --data '").Append(requestBody.Replace("'", "'\"'\"'", StringComparison.Ordinal)).Append('\'');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 构建扩展属性 JSON。
    /// </summary>
    /// <param name="traceId">追踪 Id。</param>
    /// <param name="correlationId">关联 Id。</param>
    /// <returns>扩展属性 JSON。</returns>
    private static string BuildExtraPropertiesJson(string traceId, string correlationId) {
        var properties = new Dictionary<string, string> {
            ["traceId"] = traceId,
            ["correlationId"] = correlationId
        };

        return JsonSerializer.Serialize(properties, JsonSerializerOptions);
    }

    /// <summary>
    /// 正文采集结果。
    /// </summary>
    /// <param name="Content">正文内容。</param>
    /// <param name="HasBody">是否存在正文。</param>
    /// <param name="IsTruncated">是否截断。</param>
    /// <param name="OriginalLengthBytes">原始字节长度。</param>
    private readonly record struct CapturedBody(
        string Content,
        bool HasBody,
        bool IsTruncated,
        long OriginalLengthBytes) {
        /// <summary>
        /// 空正文采集结果。
        /// </summary>
        public static CapturedBody Empty => new(string.Empty, false, false, 0L);
    }
}
