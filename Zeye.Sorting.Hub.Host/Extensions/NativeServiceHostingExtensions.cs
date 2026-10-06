using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Zeye.Sorting.Hub.Host.Extensions;

/// <summary>为一体化发布程序接入 Windows 服务或 Linux systemd 生命周期。</summary>
public static class NativeServiceHostingExtensions {
    /// <summary>按当前平台注册服务生命周期；直接启动时继续使用控制台生命周期。</summary>
    public static void AddNativeServiceLifetime(this IHostApplicationBuilder builder) {
        if (OperatingSystem.IsWindows()) {
            ConfigureWindowsService(builder);
        }
        else if (OperatingSystem.IsLinux()) {
            ConfigureSystemdService(builder);
        }
    }

    /// <summary>匹配安装脚本指定的服务名，并将服务的相对文件路径定位到发布目录。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConfigureWindowsService(IHostApplicationBuilder builder) {
        if (WindowsServiceHelpers.IsWindowsService()) {
            // 服务控制管理器的默认工作目录是 System32，不能用于保存业务日志和备份。
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        }
        builder.Services.AddWindowsService(options =>
            options.ServiceName = builder.Configuration["ServiceName"] ?? "Zeye.Sorting.Hub.Host");
    }

    /// <summary>启用 systemd 的就绪、停止通知；非 systemd 启动时保持控制台行为。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ConfigureSystemdService(IHostApplicationBuilder builder) => builder.Services.AddSystemd();
}
