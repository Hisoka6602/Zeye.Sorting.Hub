using Zeye.Sorting.Hub.Host;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// HostingOptions 配置拼装测试。
/// </summary>
public sealed class HostingOptionsTests {
    /// <summary>
    /// 验证场景：监听地址可按分号拆分并去重。
    /// </summary>
    [Fact]
    public void GetUrlBindings_ShouldSplitAndDistinct() {
        var options = new HostingOptions {
            Urls = "http://localhost:5078; http://localhost:5078 ;https://localhost:7078"
        };

        var bindings = options.GetUrlBindings();

        Assert.Equal(2, bindings.Count);
        Assert.Contains("http://localhost:5078", bindings);
        Assert.Contains("https://localhost:7078", bindings);
    }

    /// <summary>
    /// 验证场景：浏览器自动打开地址可由监听地址与 Swagger 前缀组合，且 0.0.0.0 自动归一化为 localhost。
    /// </summary>
    [Fact]
    public void BuildBrowserAutoOpenUrl_ShouldUseLocalhostAndRoutePrefix() {
        var options = new HostingOptions {
            Urls = "http://0.0.0.0:5078",
            Swagger = new SwaggerOptions {
                RoutePrefix = "docs/swagger"
            }
        };

        var url = options.BuildBrowserAutoOpenUrl();

        Assert.Equal("http://localhost:5078/docs/swagger/", url);
    }

    /// <summary>
    /// 验证场景：显式配置 BrowserAutoOpen.Url 时优先使用配置值。
    /// </summary>
    [Fact]
    public void BuildBrowserAutoOpenUrl_ShouldPreferConfiguredUrl() {
        var options = new HostingOptions {
            Urls = "http://0.0.0.0:5078",
            BrowserAutoOpen = new BrowserAutoOpenOptions {
                Enabled = true,
                Url = "http://localhost:5078/custom-swagger"
            }
        };

        var url = options.BuildBrowserAutoOpenUrl();

        Assert.Equal("http://localhost:5078/custom-swagger", url);
    }

    /// <summary>
    /// 验证场景：未配置可解析监听地址时返回 null，交由托管服务跳过副作用。
    /// </summary>
    [Fact]
    public void BuildBrowserAutoOpenUrl_ShouldReturnNull_WhenBindingsAreInvalid() {
        var options = new HostingOptions {
            Urls = "not-a-valid-url"
        };

        var url = options.BuildBrowserAutoOpenUrl();

        Assert.Null(url);
    }
}
