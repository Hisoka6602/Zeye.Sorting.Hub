using System.Security.Cryptography;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Security;

/// <summary>没有部署密钥时使用的本机首次管理员凭据，重启保留、完成初始化后失效。</summary>
public sealed class AdministratorBootstrapKeyStore(string keyPath) {
    /// <summary>密钥文件与配置库同目录，内容不进入日志或接口响应。</summary>
    public string KeyPath { get; } = Path.GetFullPath(keyPath);
    /// <summary>同一进程的生成、读取和清理互斥。</summary>
    private readonly object _gate = new();
    /// <summary>首次读取后缓存，避免会话查询重复访问文件。</summary>
    private string? _key;
    /// <summary>已观察到管理员存在，阻止过时的并发请求重新生成凭据。</summary>
    private bool _retired;
    /// <summary>只记录维护故障，不包含密钥。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>只有本机首次初始化入口可调用；受保护文件沿用原密钥，无法安全读取时拒绝开放。</summary>
    public string? GetOrCreate() {
        lock (_gate) {
            if (_retired) return null;
            if (_key is not null) return _key;
            try {
                if (!File.Exists(KeyPath)) {
                    var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    try { ProtectedSecretFile.Write(KeyPath, generated, overwrite: false); }
                    catch (IOException exception) when (File.Exists(KeyPath)) {
                        Logger.Debug(exception, "管理员初始化密钥已由并发启动创建，将读取已有文件。");
                    }
                }
                var stored = File.ReadAllText(KeyPath).Trim();
                if (stored.Length != 64 || !stored.All(static character => character is >= '0' and <= '9' or >= 'A' and <= 'F'))
                    throw new InvalidDataException("本机管理员初始化密钥文件格式无效。");
                return _key = stored;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException) {
                Logger.Error(exception, "无法准备本机管理员初始化密钥，File={File}", KeyPath);
                return null;
            }
        }
    }

    /// <summary>账号提交成功或已存在管理员时关闭本进程入口，并清理已失效文件。</summary>
    public void Retire() {
        lock (_gate) {
            if (_retired) return;
            _retired = true;
            _key = null;
            try { File.Delete(KeyPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
                Logger.Error(exception, "清理失效的管理员初始化密钥失败，入口已关闭，File={File}", KeyPath);
            }
        }
    }
}
