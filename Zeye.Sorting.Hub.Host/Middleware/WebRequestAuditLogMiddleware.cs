using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
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
    /// 请求体缓冲阈值（30KB），超过阈值后由框架切换到临时文件。
    /// </summary>
    private const int RequestBodyBufferThresholdBytes = 1024 * 30;
    /// <summary>
    /// 不适宜记录全文的二进制正文占位文本。
    /// </summary>
    private const string BinaryPayloadOmittedPlaceholder = "[binary payload omitted]";

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly NLog.ILogger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 下一个中间件委托。
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 审计配置。
    /// </summary>
    private readonly WebRequestAuditLogOptions _options;
    /// <summary>
    /// 后台审计队列（有界队列+背压保护）。
    /// </summary>
    private readonly WebRequestAuditBackgroundQueue _backgroundQueue;

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
    /// <param name="backgroundQueue">后台审计队列。</param>
    public WebRequestAuditLogMiddleware(
        RequestDelegate next,
        IOptions<WebRequestAuditLogOptions> options,
        WebRequestAuditBackgroundQueue backgroundQueue) {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _backgroundQueue = backgroundQueue ?? throw new ArgumentNullException(nameof(backgroundQueue));
    }

    /// <summary>
    /// 执行中间件。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <returns>异步任务。</returns>
    public async Task InvokeAsync(HttpContext context) {
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
            try {
                requestBodyCapture = await CaptureRequestBodyAsync(context.Request, _options.MaxRequestBodyLength);
                requestSizeBytes = requestBodyCapture.OriginalLengthBytes;
            }
            catch (Exception exception) {
                NLogLogger.Error(exception, "请求体采集失败，降级为空正文采集，Path={Path}, TraceId={TraceId}", context.Request.Path, traceId);
                requestBodyCapture = new CapturedBody(string.Empty, context.Request.ContentLength is > 0L, false, context.Request.ContentLength ?? 0L);
            }
        }

        var originalResponseBody = context.Response.Body;
        var responseCaptureStream = _options.IncludeResponseBody
            ? new ResponseCaptureTeeStream(originalResponseBody, _options.MaxResponseBodyLength)
            : null;
        if (responseCaptureStream is not null) {
            context.Response.Body = responseCaptureStream;
        }

        ExceptionDispatchInfo? exceptionDispatchInfo = null;
        try {
            // 步骤 3：继续执行后续管线并记录异常信息。
            await _next(context);
            routeTemplate = ResolveRouteTemplate(context, routeTemplate);
        }
        catch (Exception ex) {
            capturedException = ex;
            exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            routeTemplate = ResolveRouteTemplate(context, routeTemplate);
            NLogLogger.Error(ex, "Web 请求审计过程中发生管道执行异常，Path={Path}, TraceId={TraceId}", context.Request.Path, traceId);
        }
        finally {
            // 步骤 4：恢复响应流并完成响应体采集（失败不得污染主请求）。
            if (responseCaptureStream is not null) {
                try {
                    try {
                        var responseCaptureResult = responseCaptureStream.BuildCaptureResult();
                        responseBodyCapture = new CapturedBody(
                            responseCaptureResult.Content ?? string.Empty,
                            responseCaptureResult.HasBody,
                            responseCaptureResult.IsTruncated,
                            responseCaptureResult.TotalBytes);
                    }
                    catch (Exception exception) {
                        NLogLogger.Error(exception, "响应体采集失败，降级为空正文采集，Path={Path}, TraceId={TraceId}", context.Request.Path, traceId);
                        responseBodyCapture = new CapturedBody(string.Empty, context.Response.ContentLength is > 0L, false, context.Response.ContentLength ?? 0L);
                    }
                }
                finally {
                    context.Response.Body = originalResponseBody;
                    try {
                        await responseCaptureStream.DisposeAsync();
                    }
                    catch (Exception exception) {
                        NLogLogger.Error(exception, "响应体采集流释放失败，已降级忽略，Path={Path}, TraceId={TraceId}", context.Request.Path, traceId);
                    }
                }
            }

            // 步骤 5：后台异步写审计，不等待写库完成，确保不阻塞主请求返回。
            var endedAt = DateTime.Now;
            var durationMs = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;
            var isSuccess = statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status400BadRequest;
            var resolvedException = capturedException ?? context.Features.Get<IExceptionHandlerFeature>()?.Error;
            try {
                var detail = BuildDetail(
                    context,
                    startedAt,
                    requestBodyCapture,
                    responseBodyCapture,
                    resolvedException,
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
                    HasException = resolvedException is not null,
                    AuditResourceType = AuditResourceType.Api,
                    ResourceId = context.Request.Path.Value ?? string.Empty,
                    StartedAt = startedAt,
                    EndedAt = endedAt,
                    DurationMs = Math.Max(0L, durationMs),
                    CreatedAt = endedAt,
                    Detail = detail
                };
                if (!_backgroundQueue.TryEnqueue(new WebRequestAuditBackgroundEntry {
                        Log = log,
                        TraceId = traceId,
                        CorrelationId = correlationId
                    })) {
                    NLogLogger.Warn("Web 请求审计入队失败，已触发丢弃保护。TraceId={TraceId}, CorrelationId={CorrelationId}", traceId, correlationId);
                }
            }
            catch (Exception exception) {
                NLogLogger.Error(exception, "Web 请求审计构建或入队失败，已降级忽略，Path={Path}, TraceId={TraceId}, CorrelationId={CorrelationId}", context.Request.Path, traceId, correlationId);
            }
        }

        exceptionDispatchInfo?.Throw();
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
            RequestUrl = requestUrl ?? string.Empty,
            RequestQueryString = context.Request.QueryString.Value ?? string.Empty,
            RequestHeadersJson = requestHeaders ?? string.Empty,
            ResponseHeadersJson = responseHeaders ?? string.Empty,
            RequestContentType = context.Request.ContentType ?? string.Empty,
            ResponseContentType = context.Response.ContentType ?? string.Empty,
            Accept = context.Request.Headers.Accept.ToString() ?? string.Empty,
            Referer = context.Request.Headers.Referer.ToString() ?? string.Empty,
            Origin = context.Request.Headers.Origin.ToString() ?? string.Empty,
            AuthorizationType = ResolveAuthorizationType(context.Request.Headers.Authorization.ToString()),
            UserAgent = context.Request.Headers.UserAgent.ToString() ?? string.Empty,
            RequestBody = requestBodyCapture.Content ?? string.Empty,
            ResponseBody = responseBodyCapture.Content ?? string.Empty,
            CurlCommand = BuildCurlCommand(context.Request, requestBodyCapture),
            ErrorMessage = errorMessage ?? string.Empty,
            ExceptionType = exceptionType ?? string.Empty,
            ExceptionStackTrace = exceptionStackTrace ?? string.Empty,
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
        if (request.Body == Stream.Null || !request.Body.CanRead) {
            return CapturedBody.Empty;
        }

        var knownLength = Math.Max(0L, request.ContentLength ?? 0L);
        var contentType = request.ContentType ?? string.Empty;
        var hasBody = request.ContentLength is > 0L;
        if (!string.IsNullOrWhiteSpace(contentType) && !IsTextualRequestContentType(contentType)) {
            return new CapturedBody(hasBody ? BinaryPayloadOmittedPlaceholder : string.Empty, hasBody, false, knownLength);
        }

        if (maxLength <= 0) {
            var bodySize = request.ContentLength ?? 0L;
            return new CapturedBody(string.Empty, hasBody, hasBody, bodySize);
        }

        var safeLength = Math.Min(maxLength, int.MaxValue - 1);
        var bufferLimit = Math.Max(1L, Encoding.UTF8.GetMaxByteCount(safeLength));
        if (request.ContentLength is > 0L) {
            var contentLength = request.ContentLength.Value;
            if (contentLength > bufferLimit) {
                return new CapturedBody(string.Empty, true, true, contentLength);
            }
        }

        var bufferThreshold = Math.Min(RequestBodyBufferThresholdBytes, bufferLimit);
        request.EnableBuffering((int)bufferThreshold, bufferLimit);
        request.Body.Position = 0;
        try {
            var content = await ReadBodyWithinLimitAsync(request.Body, maxLength);
            request.Body.Position = 0;
            if (request.ContentLength is > 0L) {
                return content with { OriginalLengthBytes = request.ContentLength.Value };
            }

            return content;
        }
        catch (IOException ex) {
            NLogLogger.Error(ex, "请求体缓冲过程中发生 I/O 异常，Path={Path}, MaxLength={MaxLength}", request.Path, maxLength);
            request.Body.Position = 0;
            return new CapturedBody(string.Empty, hasBody, hasBody, knownLength);
        }
        catch (Exception ex) {
            NLogLogger.Error(ex, "请求体缓冲过程中发生异常，Path={Path}, MaxLength={MaxLength}", request.Path, maxLength);
            request.Body.Position = 0;
            return new CapturedBody(string.Empty, hasBody, hasBody, knownLength);
        }
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
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in headers) {
            values[pair.Key] = pair.Value.ToString();
        }

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
    /// <param name="requestBodyCapture">请求体采集结果。</param>
    /// <returns>Curl 命令。</returns>
    private static string BuildCurlCommand(HttpRequest request, CapturedBody requestBodyCapture) {
        var builder = new StringBuilder();
        var method = string.IsNullOrWhiteSpace(request.Method) ? "GET" : request.Method;
        builder.Append("curl -X ").Append(method).Append(' ');
        builder.Append(ShellEscapeSingleQuoted(BuildRequestUrl(request)));

        if (!string.IsNullOrWhiteSpace(request.ContentType)) {
            builder.Append(" -H ").Append(ShellEscapeSingleQuoted($"Content-Type: {request.ContentType}"));
        }

        var accept = request.Headers.Accept.ToString();
        if (!string.IsNullOrWhiteSpace(accept)) {
            builder.Append(" -H ").Append(ShellEscapeSingleQuoted($"Accept: {accept}"));
        }

        var userAgent = request.Headers.UserAgent.ToString();
        if (!string.IsNullOrWhiteSpace(userAgent)) {
            builder.Append(" -H ").Append(ShellEscapeSingleQuoted($"User-Agent: {userAgent}"));
        }

        var authorization = request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) {
            builder.Append(" -H ").Append(ShellEscapeSingleQuoted($"Authorization: {authorization}"));
        }

        foreach (var header in request.Headers) {
            if (!ShouldIncludeInCurl(header.Key)) {
                continue;
            }

            var value = header.Value.ToString();
            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            builder.Append(" -H ").Append(ShellEscapeSingleQuoted($"{header.Key}: {value}"));
        }

        if (requestBodyCapture.HasBody) {
            builder.Append(" --data-raw ").Append(ShellEscapeSingleQuoted(requestBodyCapture.Content ?? string.Empty));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 判断请求 Content-Type 是否适宜按文本采集。
    /// </summary>
    /// <param name="contentType">请求 Content-Type。</param>
    /// <returns>适宜文本采集返回 true。</returns>
    private static bool IsTextualRequestContentType(string contentType) {
        if (string.IsNullOrWhiteSpace(contentType)) {
            return false;
        }

        var normalized = contentType.ToLowerInvariant();
        var mediaType = ExtractMediaType(normalized);
        if (mediaType == "multipart/form-data"
            || mediaType == "application/octet-stream") {
            return false;
        }

        return mediaType.StartsWith("text/", StringComparison.Ordinal)
               || mediaType == "application/json"
               || mediaType == "application/xml"
               || mediaType == "application/x-www-form-urlencoded"
               || mediaType == "application/javascript";
    }

    /// <summary>
    /// 提取 Content-Type 主类型（去除分号参数）。
    /// </summary>
    /// <param name="contentType">原始 Content-Type。</param>
    /// <returns>主类型文本。</returns>
    private static string ExtractMediaType(string contentType) {
        var separatorIndex = contentType.IndexOf(';', StringComparison.Ordinal);
        return separatorIndex >= 0 ? contentType[..separatorIndex].Trim() : contentType.Trim();
    }

    /// <summary>
    /// 判断请求头是否允许写入 Curl 命令。
    /// </summary>
    /// <param name="headerName">请求头名。</param>
    /// <returns>允许写入返回 true。</returns>
    private static bool ShouldIncludeInCurl(string headerName) {
        if (string.IsNullOrWhiteSpace(headerName)) {
            return false;
        }

        return !headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Accept", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Host", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase)
               && !headerName.Equals("Connection", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 对 shell 单引号字符串进行安全转义。
    /// </summary>
    /// <param name="value">待转义文本。</param>
    /// <returns>转义后的 shell 参数。</returns>
    private static string ShellEscapeSingleQuoted(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "''";
        }

        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
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
