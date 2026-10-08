using System.Globalization;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>将单主机 TCP Easy Connect 转为等价描述符，避免断网时解析失败被驱动缓存为无效别名。</summary>
public static partial class OracleConnectionStringNormalization {
    /// <summary>只转换明确的单主机服务地址；复杂地址、TLS 参数、钱包及 TNS 别名交给原驱动处理。</summary>
    [GeneratedRegex(@"^(?://)?(?<host>[A-Za-z0-9._-]+)(?::(?<port>[0-9]{1,5}))?/(?<service>[A-Za-z0-9._-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleAddress();

    /// <summary>纯内存处理，不查询 DNS，不建立连接，不记录或改变认证字段。</summary>
    public static string Normalize(string connectionString) {
        var builder = new OracleConnectionStringBuilder(connectionString);
        var match = SimpleAddress().Match(builder.DataSource);
        if (!match.Success) return connectionString;
        var port = match.Groups["port"].Success
            ? int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture) : 1521;
        if (port is < 1 or > 65535) return connectionString;
        builder.DataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={match.Groups["host"].Value})(PORT={port.ToString(CultureInfo.InvariantCulture)}))(CONNECT_DATA=(SERVICE_NAME={match.Groups["service"].Value})))";
        return builder.ConnectionString;
    }
}
