using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Configuration;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>独立的 LiteDB 当前配置和 SQLite 历史文件，不依赖外部 MySQL。</summary>
public sealed class ConfigurationTestStorage : IDisposable {
    /// <summary>本测试创建的唯一目录。</summary>
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "zeye-configuration-test-" + Guid.NewGuid().ToString("N"));
    /// <summary>耐久的配置变更原值历史。</summary>
    public ConfigurationHistoryStore History { get; }
    /// <summary>共享模式配置数据库。</summary>
    public LiteDbConfigurationStore Store { get; }
    /// <summary>绑定 options 的配置源。</summary>
    public RuntimeConfigurationProvider Source { get; }
    /// <summary>测试使用的统一配置根。</summary>
    public IConfigurationRoot Configuration { get; }
    /// <summary>从内置默认配置建立独立数据库。</summary>
    public ConfigurationTestStorage() {
        History = new(Path.Combine(DirectoryPath, "history.db"));
        Store = new(Path.Combine(DirectoryPath, "settings.db"), History, ConfigurationDocument.Defaults(), new());
        Source = new(Store);
        Source.Validating += HostConfigurationValidator.Validate;
        Configuration = new ConfigurationBuilder().Add(Source).Build();
        Source.UseOverrides(Configuration);
    }
    /// <summary>只删除此测试创建的目录，并检查最终绝对路径。</summary>
    public void Dispose() {
        ((IDisposable)Configuration).Dispose(); Store.Dispose();
        var expected = Path.Combine(Path.GetTempPath(), "zeye-configuration-test-");
        if (!Path.GetFullPath(DirectoryPath).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("测试清理路径无效。");
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
