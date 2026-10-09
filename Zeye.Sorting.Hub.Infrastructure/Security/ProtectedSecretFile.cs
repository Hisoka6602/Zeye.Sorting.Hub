using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Zeye.Sorting.Hub.Infrastructure.Security;

/// <summary>本机引导密钥的文件访问控制，先限制权限再写入秘密。</summary>
public static class ProtectedSecretFile {
    /// <summary>仅允许运行账号和管理员读取；禁止覆盖时使用原子创建语义。</summary>
    public static void Write(string path, string secret, bool overwrite = true) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        FileStream stream;
        FileSecurity? security = null;
        if (OperatingSystem.IsWindows()) {
            using var identity = WindowsIdentity.GetCurrent();
            security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
            // 新文件在创建时获得受限 ACL；已有文件先收紧访问控制再覆盖。
            var file = new FileInfo(path);
            if (overwrite && file.Exists) file.SetAccessControl(security);
            stream = file.Create(mode, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
        }
        else { stream = new FileStream(path, mode, FileAccess.Write, FileShare.None); }
        using (stream) {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
            writer.Write(secret);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }
}
