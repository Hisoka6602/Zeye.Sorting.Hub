using Oracle.ManagedDataAccess.Client;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>标准描述符转换必须保留连接语义和认证选项，复杂 Oracle 地址保持兼容。</summary>
public sealed class OracleConnectionNormalizationTests {
    /// <summary>单主机地址转换后保留服务名、显式或默认端口，以及带特殊字符的认证字段。</summary>
    [Theory]
    [InlineData("oracle:1521/FREEPDB1", "oracle", "1521", "FREEPDB1")]
    [InlineData("//db.example.test/warehouse.prod", "db.example.test", "1521", "warehouse.prod")]
    [InlineData("127.0.0.1:15236/FREEPDB1", "127.0.0.1", "15236", "FREEPDB1")]
    public void SimpleAddressesPreserveServiceAndConnectionOptions(string address, string host, string port, string service) {
        var original = new OracleConnectionStringBuilder {
            DataSource = address, UserID = "test-user", Password = "test;password\"value",
            ConnectionTimeout = 15, Pooling = true, MaxPoolSize = 60
        };
        var actual = new OracleConnectionStringBuilder(OracleConnectionStringNormalization.Normalize(original.ConnectionString));
        Assert.Equal($"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={service})))", actual.DataSource);
        Assert.Equal(original.UserID, actual.UserID); Assert.Equal(original.Password, actual.Password);
        Assert.Equal(original.ConnectionTimeout, actual.ConnectionTimeout);
        Assert.Equal(original.Pooling, actual.Pooling); Assert.Equal(original.MaxPoolSize, actual.MaxPoolSize);
        Assert.Equal(actual.ConnectionString, OracleConnectionStringNormalization.Normalize(actual.ConnectionString));
    }

    /// <summary>别名、TLS、集群、IPv6及额外命名参数不能被简化或剥离。</summary>
    [Theory]
    [InlineData("WAREHOUSE_ALIAS")]
    [InlineData("tcps://db.example.test:2484/service?wallet_location=/wallet")]
    [InlineData("db-a,db-b:1521/service")]
    [InlineData("[::1]:1521/service")]
    [InlineData("db/service:dedicated/instance")]
    [InlineData("db:0/service")]
    [InlineData("db:65536/service")]
    [InlineData("db/service?connect_timeout=10")]
    public void AdvancedAddressesRemainUnchanged(string address) {
        var connection = new OracleConnectionStringBuilder { DataSource = address, UserID = "test", Password = "test" }.ConnectionString;
        Assert.Equal(connection, OracleConnectionStringNormalization.Normalize(connection));
    }
}
