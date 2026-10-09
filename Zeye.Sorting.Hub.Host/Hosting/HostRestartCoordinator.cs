using System.Diagnostics;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Hosting;

/// <summary>在响应完成后正常关闭宿主，再由服务恢复策略或原启动命令启动新进程。</summary>
public sealed class HostRestartCoordinator(IHostApplicationLifetime application, IHostLifetime lifetime) : IDisposable {
    /// <summary>受监管进程的主动重启退出码，与普通停止区分。</summary>
    private const int RestartExitCode = 75;
    /// <summary>重复请求只安排一次重启。</summary>
    private int _requested;
    /// <summary>停止信号只发送一次。</summary>
    private int _stopping;
    /// <summary>响应丢失时仍能完成已经接受的重启。</summary>
    private Timer? _stopTimer;
    /// <summary>宿主重启请求与异常诊断，不包含配置或访问码。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>正常停止之后是否需要重新启动当前程序。</summary>
    public bool Requested => Volatile.Read(ref _requested) != 0;

    /// <summary>校验已保存版本，并在响应发送之后安排唯一一次正常停止。</summary>
    public IResult RequestRestart(HttpContext context, string? revision, RuntimeConfigurationProvider source, DatabaseStartupState state) {
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["X-Zeye-Client"] != "web" || origin.Length > 0
            && !origin.Equals($"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 403, detail: "重启请求缺少来源校验或来源不匹配。");
        source.TryReload();
        if (string.IsNullOrWhiteSpace(revision) || !string.Equals(revision, source.Capture().Revision, StringComparison.Ordinal))
            return Results.Problem(statusCode: 409, detail: "配置版本已变化，请重新读取已保存配置后再重启。");
        if (!Requested && application.ApplicationStopping.IsCancellationRequested)
            return Results.Problem(statusCode: 409, detail: "Host 正在停止，请等待后重试。");
        if (Interlocked.CompareExchange(ref _requested, 1, 0) == 0) {
            try {
                // 配置失败时继续允许同一已授权页面修正连接；普通启动仍生成新的访问码。
                state.PreserveSetupKeyForRestart();
                _stopTimer = new Timer(_ => StopApplication(), null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                context.Response.OnCompleted(() => {
                    _stopTimer?.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
                    return Task.CompletedTask;
                });
                Logger.Warn("已接受 Host 重启，Instance={Instance}，TraceId={TraceId}", state.InstanceId, context.TraceIdentifier);
            }
            catch (Exception exception) {
                Interlocked.Exchange(ref _requested, 0);
                Logger.Error(exception, "安排 Host 重启失败，当前宿主继续运行。");
                return Results.Problem(statusCode: 503, detail: "暂时无法安排重启，请检查服务日志和配置目录权限。");
            }
        }
        context.Response.Headers.CacheControl = "no-store";
        return Results.Json(new { instanceId = state.InstanceId, restarting = true }, statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>Windows 服务以非零状态正常关闭，确保已安装的恢复策略执行自动启动。</summary>
    private void StopApplication() {
        if (Interlocked.Exchange(ref _stopping, 1) != 0) return;
        if (OperatingSystem.IsWindows() && lifetime is WindowsServiceLifetime windows) windows.ExitCode = RestartExitCode;
        application.StopApplication();
    }

    /// <summary>宿主和配置存储释放后，受监管实例交给监管器；直接运行则使用原命令重新启动。</summary>
    public void CompleteRestart() {
        if (!Requested) return;
        var managed = OperatingSystem.IsWindows() && lifetime is WindowsServiceLifetime
            || OperatingSystem.IsLinux() && (SystemdHelpers.IsSystemdService() || Environment.ProcessId == 1
                || File.Exists("/.dockerenv") || File.Exists("/run/.containerenv"));
        if (managed) { Environment.ExitCode = RestartExitCode; return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定 Host 的启动程序。");
        var command = Environment.GetCommandLineArgs();
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
        // dotnet 承载模式需要程序集参数；自包含 apphost 只保留原应用参数。
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(command[0]);
        foreach (var argument in command.Skip(1)) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Host 新进程未能启动。");
        Logger.Info("已启动新的 Host 进程，ProcessId={ProcessId}", process.Id);
    }

    /// <summary>宿主正常停止后释放已经完成的重启定时器。</summary>
    public void Dispose() => _stopTimer?.Dispose();
}
