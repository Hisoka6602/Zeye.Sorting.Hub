using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>重现故障断开后 HTTP 上下文已释放、SignalR 连接身份仍可读取的状态。</summary>
internal sealed class DisposedHubCallerContext : HubCallerContext, IHttpContextFeature {
    /// <summary>构造真实已释放的 DefaultHttpContext，并通过连接特性提供该对象。</summary>
    public DisposedHubCallerContext() {
        var context = new DefaultHttpContext();
        context.Uninitialize();
        HttpContext = context;
        Features.Set<IHttpContextFeature>(this);
    }
    /// <inheritdoc />
    public override string ConnectionId => "disposed-context-connection";
    /// <inheritdoc />
    public override string? UserIdentifier => null;
    /// <inheritdoc />
    public override ClaimsPrincipal? User => null;
    /// <inheritdoc />
    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    /// <inheritdoc />
    public override IFeatureCollection Features { get; } = new FeatureCollection();
    /// <inheritdoc />
    public override CancellationToken ConnectionAborted => CancellationToken.None;
    /// <inheritdoc />
    public HttpContext? HttpContext { get; set; }
    /// <summary>测试中断不影响其他测试连接。</summary>
    public override void Abort() { }
}
