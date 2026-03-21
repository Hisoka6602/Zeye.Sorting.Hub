using System.Diagnostics;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// Development 环境浏览器启动隔离器。
/// </summary>
public sealed class DevelopmentBrowserLauncherHostedService : IHostedService {
    /// <summary>
    /// 宿主环境信息。
    /// </summary>
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Host 启动配置。
    /// </summary>
    private readonly IOptions<HostingOptions> _hostingOptions;

    /// <summary>
    /// 安全执行器（用于隔离副作用异常）。
    /// </summary>
    private readonly SafeExecutor _safeExecutor;

    /// <summary>
    /// 应用生命周期（用于在应用真正启动后执行副作用）。
    /// </summary>
    private readonly IHostApplicationLifetime _applicationLifetime;

    /// <summary>
    /// 日志器。
    /// </summary>
    private readonly ILogger<DevelopmentBrowserLauncherHostedService> _logger;

    /// <summary>
    /// ApplicationStarted 回调注册句柄。
    /// </summary>
    private IDisposable? _applicationStartedRegistration;

    /// <summary>
    /// 浏览器打开动作是否已触发（防止重复执行）。
    /// </summary>
    private int _browserLaunchTriggered;

    /// <summary>
    /// 初始化 Development 浏览器启动隔离器。
    /// </summary>
    /// <param name="environment">宿主环境。</param>
    /// <param name="hostingOptions">Host 配置。</param>
    /// <param name="applicationLifetime">应用生命周期。</param>
    /// <param name="safeExecutor">安全执行器。</param>
    /// <param name="logger">日志器。</param>
    public DevelopmentBrowserLauncherHostedService(
        IHostEnvironment environment,
        IOptions<HostingOptions> hostingOptions,
        IHostApplicationLifetime applicationLifetime,
        SafeExecutor safeExecutor,
        ILogger<DevelopmentBrowserLauncherHostedService> logger) {
        _environment = environment;
        _hostingOptions = hostingOptions;
        _applicationLifetime = applicationLifetime;
        _safeExecutor = safeExecutor;
        _logger = logger;
    }

    /// <summary>
    /// 启动服务并注册“应用启动完成后”再打开浏览器的回调。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public Task StartAsync(CancellationToken cancellationToken) {
        var options = _hostingOptions.Value;
        if (!_environment.IsDevelopment()) {
            return Task.CompletedTask;
        }

        if (!options.BrowserAutoOpen.Enabled) {
            return Task.CompletedTask;
        }
        if (!options.Swagger.Enabled) {
            _logger.LogInformation("跳过自动打开浏览器：Swagger 未启用。");
            return Task.CompletedTask;
        }

        if (!CanRunBrowserSideEffect()) {
            _logger.LogInformation("跳过自动打开浏览器：当前环境非交互式、容器/CI 或不支持图形化浏览器。");
            return Task.CompletedTask;
        }

        _applicationStartedRegistration = _applicationLifetime.ApplicationStarted.Register(() => {
            if (Interlocked.Exchange(ref _browserLaunchTriggered, 1) == 1) {
                return;
            }

            TryLaunchBrowser(_hostingOptions.Value);
        });
        _logger.LogInformation("Development 浏览器自动打开已注册为 ApplicationStarted 回调。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public Task StopAsync(CancellationToken cancellationToken) {
        Interlocked.Exchange(ref _applicationStartedRegistration, null)?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 判断当前进程是否允许执行浏览器副作用。
    /// </summary>
    /// <returns>允许执行时返回 true；否则返回 false。</returns>
    private static bool CanRunBrowserSideEffect() {
        var isRunningInContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isCi = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"));
        return Environment.UserInteractive && !OperatingSystem.IsBrowser() && !isRunningInContainer && !isCi;
    }

    /// <summary>
    /// 在应用已启动后尝试打开本机浏览器。
    /// </summary>
    /// <param name="options">Host 配置。</param>
    private void TryLaunchBrowser(HostingOptions options) {
        var targetUrl = options.BuildBrowserAutoOpenUrl();
        if (string.IsNullOrWhiteSpace(targetUrl)) {
            _logger.LogWarning("跳过自动打开浏览器：未能从配置推导有效地址。");
            return;
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri)) {
            _logger.LogWarning("跳过自动打开浏览器：自动打开地址不是有效绝对地址，Url={SwaggerUrl}", targetUrl);
            return;
        }

        if (!targetUri.IsLoopback && !string.Equals(targetUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) {
            _logger.LogInformation("跳过自动打开浏览器：仅允许本机地址，当前 Url={SwaggerUrl}", targetUrl);
            return;
        }

        var launchSuccess = _safeExecutor.Execute(
            () => {
                var startInfo = new ProcessStartInfo {
                    FileName = targetUrl,
                    UseShellExecute = true
                };
                var process = Process.Start(startInfo);
                _logger.LogInformation(
                    "已在 Development 环境触发浏览器自动打开：{SwaggerUrl}，进程创建：{ProcessStarted}",
                    targetUrl,
                    process is not null);
            },
            "Development 启动自动打开 Swagger 页面");
        if (!launchSuccess) {
            _logger.LogWarning("Development 浏览器自动打开执行失败，已由 SafeExecutor 隔离。");
        }
    }
}
