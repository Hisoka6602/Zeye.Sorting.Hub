using Azure;
using NLog;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;

namespace Zeye.Sorting.Hub.Host.Authentication {

    /// <summary>
    /// 兜底认证处理器（隔离器）：当未接入真实认证方案时，提供统一 401 Challenge，避免因缺失 IAuthenticationService 导致 500。
    /// </summary>
    public sealed class GuardedAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
        /// <summary>
        /// 认证隔离器日志记录器。
        /// </summary>
        private static readonly Logger AuthenticationLogger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 默认认证方案名。
        /// </summary>
        public const string SchemeName = "GuardedAuth";

        /// <summary>
        /// 初始化处理器。
        /// </summary>
        public GuardedAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) {
        }

        /// <summary>
        /// 认证阶段：默认返回未认证，交由授权中间件触发 challenge。
        /// </summary>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        /// <summary>
        /// Challenge 阶段：显式返回 401，避免抛出服务缺失异常。
        /// </summary>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties) {
            AuthenticationLogger.Warn("认证挑战触发，返回 401，路径：{Path}", Request.Path);
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Forbid 阶段：显式返回 403。
        /// </summary>
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties) {
            AuthenticationLogger.Warn("认证禁止触发，返回 403，路径：{Path}", Request.Path);
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
