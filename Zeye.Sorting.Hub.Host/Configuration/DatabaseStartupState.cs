using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Zeye.Sorting.Hub.Host.Configuration;

/// <summary>区分网页可用与业务数据库就绪，并保护本机数据库配置入口。</summary>
public sealed class DatabaseStartupState(string configurationPath) {
    /// <summary>配置入口使用的进程级随机访问码，不写入日志或配置响应。</summary>
    private string? _setupKey;
    /// <summary>配置入口访问码的受保护本地文件。</summary>
    public string SetupKeyPath { get; } = Path.Combine(Path.GetDirectoryName(configurationPath)!, "database-setup.key");
    /// <summary>数据库初始化和业务服务启动是否全部完成。</summary>
    public bool Ready { get; private set; }
    /// <summary>是否仅提供数据库配置，拒绝业务请求。</summary>
    public bool RequiresConfiguration { get; private set; }
    /// <summary>密钥文件维护异常日志，不包含访问码。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>启动失败后生成访问码；只有运行账号和管理员能够读取。</summary>
    public void RequireConfiguration() {
        Ready = false;
        _setupKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Directory.CreateDirectory(Path.GetDirectoryName(SetupKeyPath)!);
        // 先建立访问控制再写入秘密，避免新文件短暂继承开放权限。
        using (var stream = new FileStream(SetupKeyPath, FileMode.Create, FileAccess.Write, FileShare.None)) { }
        if (OperatingSystem.IsWindows()) {
            using var identity = WindowsIdentity.GetCurrent();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(SetupKeyPath).SetAccessControl(security);
        }
        else { File.SetUnixFileMode(SetupKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        File.WriteAllText(SetupKeyPath, _setupKey);
        RequiresConfiguration = true;
        Logger.Warn("数据库未就绪，仅开放本机配置入口，访问码文件：{Path}", SetupKeyPath);
    }

    /// <summary>业务启动成功后关闭配置入口并使旧访问码失效。</summary>
    public void MarkReady() {
        _setupKey = null;
        RequiresConfiguration = false;
        Ready = true;
        try { File.Delete(SetupKeyPath); }
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

    /// <summary>访问码使用固定时间比较，且只在数据库未就绪期间有效。</summary>
    public bool Authorize(string key) => RequiresConfiguration && _setupKey is not null && key.Length == _setupKey.Length
        && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(key), System.Text.Encoding.ASCII.GetBytes(_setupKey));
}
