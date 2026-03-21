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
    /// 日志器。
    /// </summary>
    private readonly ILogger<DevelopmentBrowserLauncherHostedService> _logger;

    /// <summary>
    /// 初始化 Development 浏览器启动隔离器。
    /// </summary>
    /// <param name="environment">宿主环境。</param>
    /// <param name="hostingOptions">Host 配置。</param>
    /// <param name="safeExecutor">安全执行器。</param>
    /// <param name="logger">日志器。</param>
    public DevelopmentBrowserLauncherHostedService(
        IHostEnvironment environment,
        IOptions<HostingOptions> hostingOptions,
        SafeExecutor safeExecutor,
        ILogger<DevelopmentBrowserLauncherHostedService> logger) {
        _environment = environment;
        _hostingOptions = hostingOptions;
        _safeExecutor = safeExecutor;
        _logger = logger;
    }

    /// <summary>
    /// 启动服务并按条件尝试打开浏览器。
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

        var isRunningInContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var isCi = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI"));
        if (!Environment.UserInteractive || OperatingSystem.IsBrowser() || isRunningInContainer || isCi) {
            _logger.LogInformation("跳过自动打开浏览器：当前环境非交互式、容器/CI 或不支持图形化浏览器。");
            return Task.CompletedTask;
        }

        var targetUrl = options.BuildBrowserAutoOpenUrl();
        if (string.IsNullOrWhiteSpace(targetUrl)) {
            _logger.LogWarning("跳过自动打开浏览器：未能从配置推导有效地址。");
            return Task.CompletedTask;
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

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}
