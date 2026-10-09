using Microsoft.AspNetCore.DataProtection;
using Zeye.Sorting.Hub.Host.Authentication;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>配置卷保留密钥及旧部署凭据的升级兼容验证。</summary>
public sealed class DataProtectionKeyStorageTests {
    /// <summary>复制旧密钥后，新的保护器仍能解密原配置，重复启动也不覆盖密钥文件。</summary>
    [Fact]
    public void LegacyKeyMigrationPreservesEncryptedConfiguration() {
        var root = Path.Combine(Path.GetTempPath(), "zeye-key-storage-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var legacy = Directory.CreateDirectory(Path.Combine(root, "logs", "data-protection"));
            var original = DataProtectionProvider.Create(legacy, options => options.SetApplicationName("Zeye.Sorting.Hub"));
            var encrypted = original.CreateProtector("upgrade-test").Protect("legacy-configuration-test");
            var database = Path.Combine(root, "custom-config", "settings.db");
            var destination = DataProtectionKeyStorage.Prepare(root, database);
            Assert.Equal(Path.Combine(root, "custom-config", "data-protection"), destination.FullName);
            Assert.Single(Directory.GetFiles(destination.FullName, "*.xml"));
            Assert.Single(Directory.GetFiles(legacy.FullName, "*.xml"));
            var restored = DataProtectionProvider.Create(DataProtectionKeyStorage.Prepare(root, database), options => options.SetApplicationName("Zeye.Sorting.Hub"));
            Assert.Equal("legacy-configuration-test", restored.CreateProtector("upgrade-test").Unprotect(encrypted));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
