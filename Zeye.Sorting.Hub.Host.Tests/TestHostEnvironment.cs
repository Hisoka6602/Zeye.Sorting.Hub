using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// IHostEnvironment 测试桩。
/// </summary>
internal sealed class TestHostEnvironment : IHostEnvironment {
    /// <summary>
    /// 测试环境配置桩，用于隔离 IHostEnvironment 依赖并注入环境名称。
    /// </summary>
    /// <param name="environmentName">环境名称。</param>
    public TestHostEnvironment(string environmentName) {
        EnvironmentName = environmentName;
        ApplicationName = "Zeye.Sorting.Hub.Host.Tests";
        ContentRootPath = AppContext.BaseDirectory;
        ContentRootFileProvider = new NullFileProvider();
    }

    /// <summary>
    /// 收集测试注入的环境名称，用于控制生产/非生产策略分支断言。
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// 提供测试宿主应用名称，确保依赖 IHostEnvironment.ApplicationName 的逻辑可稳定运行。
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 提供测试内容根目录路径，避免配置或文件定位逻辑因空路径失败。
    /// </summary>
    public string ContentRootPath { get; set; }

    /// <summary>
    /// 提供测试内容根目录文件提供器，使用空实现避免真实文件系统依赖。
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; }
}
