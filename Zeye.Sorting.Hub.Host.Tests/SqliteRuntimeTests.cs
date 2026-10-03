using Microsoft.Data.Sqlite;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 中文说明：验证 Windows 与 Linux 实际加载的 SQLite 原生库包含安全修复。
/// </summary>
public sealed class SqliteRuntimeTests
{
    /// <summary>
    /// 中文说明：直接查询运行中的原生库版本，防止包升级后仍加载有漏洞的旧库。
    /// </summary>
    [Fact]
    public async Task LoadedNativeLibraryIncludesSecurityFix()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";

        var rawVersion = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.True(Version.TryParse(rawVersion, out var version), $"无效的 SQLite 版本：{rawVersion}");
        Assert.True(version >= new Version(3, 50, 2), $"SQLite 原生库缺少安全修复：{rawVersion}");
    }
}
