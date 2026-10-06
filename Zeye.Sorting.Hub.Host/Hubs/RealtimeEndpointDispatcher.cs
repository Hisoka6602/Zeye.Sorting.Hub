using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Zeye.Sorting.Hub.Contracts.Models.Realtime;
using Zeye.Sorting.Hub.Host.Authentication;
using Zeye.Sorting.Hub.Host.Extensions;
using Zeye.Sorting.Hub.Host.Middleware;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>复用白名单读取及两个命名包裹用例的正式权限、限流和审计，不接受任意写入路径。</summary>
public sealed class RealtimeEndpointDispatcher(IServiceScopeFactory scopes) {
    /// <summary>路由注册完成后固定的正式接口管线。</summary>
    private RequestDelegate? _pipeline;
    /// <summary>宿主既有异常日志器。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>用正式端点数据源构建每次调用独立的 DI 作用域管线。</summary>
    public void Configure(WebApplication app) {
        var branch = new ApplicationBuilder(app.Services);
        branch.UseRouting();
        branch.UseWebRequestAuditLogging();
        branch.UseRateLimiter();
        branch.UseAuthentication();
        branch.UseAuthorization();
        branch.UseSortingHubAccess();
        branch.UseSortingRealtime();
        branch.UseEndpoints(routes => {
            foreach (var source in ((IEndpointRouteBuilder)app).DataSources) routes.DataSources.Add(source);
        });
        _pipeline = branch.Build();
    }

    /// <summary>仅执行 GET 白名单；用原会话 Cookie 再次验证停用状态、安全戳和当前权限。</summary>
    public Task<RealtimeResponse> ReadAsync(string path, HttpContext source, CancellationToken cancellationToken) =>
        RealtimeReadPolicy.IsAllowed(path) ? ExecuteAsync("GET", path, null, source, cancellationToken)
        : Task.FromResult(new RealtimeResponse(400, "{\"detail\":\"此资源不能通过实时通道读取。\"}"));

    /// <summary>追加处理事实只允许固定用例，原入口再次验证 parcels.write 权限和幂等身份。</summary>
    public Task<RealtimeResponse> AppendProcessingRecordAsync(string json, HttpContext source, CancellationToken cancellationToken) =>
        ValidCommandBody(json) ? ExecuteAsync("POST", "/api/admin/parcels/processing-records", json, source, cancellationToken)
        : Task.FromResult(new RealtimeResponse(413, "{\"detail\":\"实时提交正文必须为非空且不超过 4 KiB 的 JSON。\"}"));

    /// <summary>状态更新只接受正整数主键；权限、业务规则和参数验证继续由原端点执行。</summary>
    public Task<RealtimeResponse> UpdateParcelStatusAsync(string id, string json, HttpContext source, CancellationToken cancellationToken) =>
        long.TryParse(id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parcelId) && parcelId > 0 && ValidCommandBody(json)
        ? ExecuteAsync("PUT", "/api/admin/parcels/" + id, json, source, cancellationToken)
        : Task.FromResult(new RealtimeResponse(400, "{\"detail\":\"包裹编号或实时正文无效。\"}"));

    /// <summary>控制单条提交内存，较大正文由前端走原有受限 HTTP 接口。</summary>
    private static bool ValidCommandBody(string? json) => !string.IsNullOrWhiteSpace(json) && Encoding.UTF8.GetByteCount(json) <= 4096;

    /// <summary>执行已确定的用例，调用方不能提供自由方法或危险管理路径。</summary>
    private async Task<RealtimeResponse> ExecuteAsync(string method, string path, string? json, HttpContext source, CancellationToken cancellationToken) {
        if (_pipeline is null) throw new InvalidOperationException("实时读取管线尚未初始化。");
        await using var scope = scopes.CreateAsyncScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = timeout.Token };
        var separator = path.IndexOf('?');
        context.Request.Path = separator < 0 ? path : path[..separator];
        context.Request.QueryString = separator < 0 ? QueryString.Empty : new QueryString(path[separator..]);
        context.Request.Method = method;
        context.Request.Scheme = source.Request.Scheme;
        context.Request.Host = source.Request.Host;
        context.Request.Headers.Cookie = source.Request.Headers.Cookie;
        context.Request.Headers["X-Zeye-Transport"] = "signalr";
        context.Request.Headers["X-Zeye-Client"] = "web";
        context.Connection.RemoteIpAddress = source.Connection.RemoteIpAddress;
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(json ?? ""));
        context.Request.Body = body;
        context.Request.ContentLength = body.Length;
        context.Request.ContentType = "application/json";
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new RealtimeRequestBodyFeature(body.Length > 0));
        using var response = new MemoryStream();
        context.Response.Body = response;
        try {
            await _pipeline(context);
            await context.Response.BodyWriter.FlushAsync(timeout.Token);
            if (response.Length > 8 * 1024 * 1024) return new(413, "{\"detail\":\"响应过大，请缩小查询范围。\"}");
            return new(context.Response.StatusCode, Encoding.UTF8.GetString(response.GetBuffer(), 0, (int)response.Length));
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested) {
            Logger.Debug(exception, "实时请求已取消或超时，Path={Path}", context.Request.Path);
            if (cancellationToken.IsCancellationRequested) throw;
            return new(504, "{\"detail\":\"实时查询超时，请缩小范围后重试。\"}");
        }
        catch (Exception exception) {
            Logger.Error(exception, "实时请求失败，Path={Path}", context.Request.Path);
            return new(500, "{\"detail\":\"服务暂时无法处理实时查询。\"}");
        }
    }
}
