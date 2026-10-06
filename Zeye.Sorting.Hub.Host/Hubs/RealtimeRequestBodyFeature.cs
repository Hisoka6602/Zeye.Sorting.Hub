using Microsoft.AspNetCore.Http.Features;

namespace Zeye.Sorting.Hub.Host.Hubs;

/// <summary>让正式 JSON 端点识别实时命名提交的正文，仍由原端点执行参数绑定。</summary>
internal sealed class RealtimeRequestBodyFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature {
    /// <summary>正文确实存在才允许参数绑定读取，不伪造空请求。</summary>
    public bool CanHaveBody { get; } = canHaveBody;
}
