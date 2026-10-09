using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Authentication;

/// <summary>只认证已登记来源的 Bearer 机器凭据，网页 Cookie 不能授权设备入口。</summary>
public sealed class FusionMachineAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, UrlEncoder encoder,
    IFusionIngestionGateway ingress, IOptions<FusionIngestionOptions> fusion, IFusionRuntimeConfiguration? runtime = null) : AuthenticationHandler<AuthenticationSchemeOptions>(options, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, encoder) {
    /// <summary>认证基类关闭默认记录器，应用仅使用 NLog 输出不含凭据的认证诊断。</summary>
    private static readonly NLog.Logger NLogLogger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>独立机器认证方案。</summary>
    public const string SchemeName = "FusionMachine";
    /// <summary>来源已认证身份声明。</summary>
    public const string SourceClaim = "fusion-source";
    /// <summary>严格读取头部凭据；查询令牌和网页身份不参与认证。</summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        var source = Request.Headers["X-Fusion-SourceId"];
        var authorization = Request.Headers.Authorization;
        if (!(runtime?.Snapshot.Options.AllowInsecureHttp ?? fusion.Value.AllowInsecureHttp) && !Request.IsHttps || source.Count != 1 || authorization.Count != 1
            || source.ToString().Length > 96 || !authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal)
            || !ingress.Authenticate(source.ToString(), authorization.ToString().Substring(7))) {
            NLogLogger.Debug("Fusion 来源认证失败：身份无效或传输未允许。");
            return Task.FromResult(AuthenticateResult.Fail("InvalidFusionCredentials"));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SourceClaim, source.ToString())], SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
