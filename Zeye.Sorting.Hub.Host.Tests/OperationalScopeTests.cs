using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Common;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 运营边界测试。
/// </summary>
public sealed class OperationalScopeTests {
    /// <summary>
    /// 构建运营边界时应标准化并保留层级维度。
    /// </summary>
    [Fact]
    public void OperationalScopeGuard_WhenRequestIsValid_ShouldCreateNormalizedScope() {
        var scope = OperationalScopeGuard.Create(new OperationalScopeRequest {
            SiteCode = "  SITE-01  ",
            LineCode = "  LINE-A  ",
            DeviceCode = "  DVC-1001  ",
            WorkstationName = "  WS-01  "
        }, "创建运营边界");

        Assert.Equal("SITE-01", scope.SiteCode);
        Assert.Equal("LINE-A", scope.LineCode);
        Assert.Equal("DVC-1001", scope.DeviceCode);
        Assert.Equal("WS-01", scope.WorkstationName);
    }

    /// <summary>
    /// 站点编码为空时应拒绝构建运营边界。
    /// </summary>
    [Fact]
    public void OperationalScopeGuard_WhenSiteCodeMissing_ShouldThrow() {
        Assert.Throws<ArgumentException>(() => OperationalScopeGuard.Create(new OperationalScopeRequest {
            SiteCode = " ",
            LineCode = "LINE-A",
            DeviceCode = "DVC-01",
            WorkstationName = "WS-01"
        }, "创建运营边界"));
    }

    /// <summary>
    /// 工作站名称为空时应拒绝构建运营边界。
    /// </summary>
    [Fact]
    public void OperationalScopeGuard_WhenWorkstationNameMissing_ShouldThrow() {
        Assert.Throws<ArgumentException>(() => OperationalScopeGuard.Create("SITE-01", "LINE-A", "DVC-01", " ", "创建运营边界"));
    }

    /// <summary>
    /// 可选维度为空白时应归一化为 null。
    /// </summary>
    [Fact]
    public void OperationalScopeGuard_WhenOptionalCodesAreBlank_ShouldNormalizeToNull() {
        var scope = OperationalScopeGuard.Create("SITE-01", " ", null, "WS-02", "创建运营边界");

        Assert.Equal("SITE-01", scope.SiteCode);
        Assert.Null(scope.LineCode);
        Assert.Null(scope.DeviceCode);
        Assert.Equal("WS-02", scope.WorkstationName);
    }

    /// <summary>
    /// 运营边界应可映射为响应合同。
    /// </summary>
    [Fact]
    public void OperationalScopeGuard_ToResponse_ShouldMapAllFields() {
        var scope = OperationalScopeGuard.Create(new OperationalScopeRequest {
            SiteCode = "SITE-02",
            LineCode = "LINE-B",
            DeviceCode = "DVC-2002",
            WorkstationName = "WS-03"
        }, "映射运营边界响应");

        var response = OperationalScopeGuard.ToResponse(scope);

        Assert.Equal("SITE-02", response.SiteCode);
        Assert.Equal("LINE-B", response.LineCode);
        Assert.Equal("DVC-2002", response.DeviceCode);
        Assert.Equal("WS-03", response.WorkstationName);
    }
}
