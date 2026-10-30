using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证MySQL不同表名存储模式下的真实缺失项，避免Windows误报及Linux漏报。</summary>
public sealed class MySqlTableNameProbeTests {
    /// <summary>小写存储只影响实际存在项，真正未创建的分表在全部模式下都必须报告。</summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void LowercaseMetadataDoesNotHideGenuinelyMissingTables(int mode, int missingCount) {
        string[] expected = ["Parcels_202610", "Parcel_DwsMeasurements_202610", "Parcels_202611"];
        string[] existing = ["parcels_202610", "parcel_dwsmeasurements_202610"];
        var missing = MySqlDialect.FindMissingTableNames(expected, existing, mode);
        Assert.Equal(missingCount, missing.Count);
        Assert.Contains("Parcels_202611", missing);
        if (mode != 0) Assert.DoesNotContain("Parcels_202610", missing);
    }

    /// <summary>大小写敏感服务器不能把不同大小写的名称当作同一张已存在的表。</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void ServerCaseSensitivityControlsObjectIdentity(int mode, int missingCount) {
        string[] expected = ["Parcels_202610", "parcels_202610"];
        string[] existing = ["Parcels_202610"];
        var missing = MySqlDialect.FindMissingTableNames(expected, existing, mode);
        Assert.Equal(missingCount, missing.Count);
        if (mode == 0) Assert.Equal("parcels_202610", Assert.Single(missing));
    }
}
