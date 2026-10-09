using System.Net;
using System.Security.Cryptography;
using Zeye.Sorting.Hub.Infrastructure.Security;

namespace Zeye.Sorting.Hub.Host.Configuration;

/// <summary>区分网页可用与业务数据库就绪，并保护本机数据库配置入口。</summary>
public sealed class DatabaseStartupState(string configurationPath) {
    /// <summary>配置入口的随机访问码，不写入日志或配置响应。</summary>
    private string? _setupKey;
    /// <summary>每个新宿主的独立身份，前端不能把旧进程响应误认为重启成功。</summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    /// <summary>配置入口访问码的受保护本地文件。</summary>
    public string SetupKeyPath { get; } = Path.Combine(Path.GetDirectoryName(configurationPath)!, "database-setup.key");
    /// <summary>受保护且仅消费一次的主动重启访问码，最多保留五分钟。</summary>
    private string RestartKeyPath => Path.ChangeExtension(SetupKeyPath, "restart.key");
    /// <summary>数据库初始化和业务服务启动是否全部完成。</summary>
    public bool Ready { get; private set; }
    /// <summary>是否仅提供数据库配置，拒绝业务请求。</summary>
    public bool RequiresConfiguration { get; private set; }
    /// <summary>供本机配置页显示的启动阶段说明，不包含异常正文、连接字符串或凭据。</summary>
    public string? FailureSummary { get; private set; }
    /// <summary>密钥文件维护异常日志，不包含访问码。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>启动失败后生成访问码；只有运行账号和管理员能够读取。</summary>
    public void RequireConfiguration(string? failureSummary = null) {
        Ready = false;
        FailureSummary = failureSummary ?? "数据库配置或初始化尚未完成。";
        _setupKey = ConsumeRestartKey() ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        ProtectedSecretFile.Write(SetupKeyPath, _setupKey);
        RequiresConfiguration = true;
        Logger.Warn("数据库未就绪，仅开放本机配置入口，访问码文件：{Path}", SetupKeyPath);
    }

    /// <summary>主动重启的本机配置页面可以继续修正连接，普通重启不保留旧访问码。</summary>
    public void PreserveSetupKeyForRestart() {
        if (RequiresConfiguration && _setupKey is not null) ProtectedSecretFile.Write(RestartKeyPath, _setupKey);
    }

    /// <summary>一次性消费尚未过期的主动重启访问码，不改变普通启动的随机码行为。</summary>
    private string? ConsumeRestartKey() {
        if (!File.Exists(RestartKeyPath)) return null;
        try {
            var age = DateTime.Now - File.GetLastWriteTime(RestartKeyPath);
            var key = File.ReadAllText(RestartKeyPath).Trim();
            return age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(5) && key.Length == 64
                && key.All(static value => value is >= '0' and <= '9' or >= 'A' and <= 'F') ? key : null;
        }
        finally { File.Delete(RestartKeyPath); }
    }

    /// <summary>业务启动成功后关闭配置入口并使旧访问码失效。</summary>
    public void MarkReady() {
        _setupKey = null;
        RequiresConfiguration = false;
        FailureSummary = null;
        Ready = true;
        try { File.Delete(SetupKeyPath); File.Delete(RestartKeyPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            Logger.Error(exception, "清理失效的数据库配置访问码文件失败。入口已关闭。");
        }
    }

    /// <summary>配置只能直接从本机回环地址访问，拒绝代理和 DNS 重绑定主机名。</summary>
    public static bool IsLocal(HttpContext context) {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
        var host = context.Request.Host.Host.Trim('[', ']');
        return address is not null && IPAddress.IsLoopback(address)
            && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(host, out var hostAddress) && IPAddress.IsLoopback(hostAddress));
    }

    /// <summary>本机引导写请求必须携带网页来源标识，并拒绝跨源访问。</summary>
    public static bool IsLocalWrite(HttpContext context) {
        var origin = context.Request.Headers.Origin.ToString();
        return IsLocal(context) && context.Request.Headers["X-Zeye-Client"] == "web"
            && (origin.Length == 0 || origin.Equals($"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>访问码使用固定时间比较，且只在数据库未就绪期间有效。</summary>
    public bool Authorize(string key) => RequiresConfiguration && _setupKey is not null && key.Length == _setupKey.Length
        && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(key), System.Text.Encoding.ASCII.GetBytes(_setupKey));
}
