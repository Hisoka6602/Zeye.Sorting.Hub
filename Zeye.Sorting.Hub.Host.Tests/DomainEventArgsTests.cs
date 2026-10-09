using Zeye.Sorting.Hub.Domain.Events.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 领域事件载荷单元测试。
/// 验证 <see cref="ParcelScannedEvent"/> 与 <see cref="ParcelChuteAssignedEvent"/>
/// 的业务字段赋值、不可变语义（readonly record struct）以及本地时间约束。
/// </summary>
public sealed class DomainEventArgsTests {

    /// <summary>
    /// 验证场景：ParcelScannedEvent 各业务字段可正确构造并读取。
    /// </summary>
    [Fact]
    public void ParcelScannedEventShouldCarryAllBusinessFields() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 20, 10, 30, 0);

        var args = new ParcelScannedEvent {
            ParcelId = 101L,
            BarCodes = "BC-TEST-001",
            WorkstationName = "WS-A1",
            ScannedTime = scannedTime,
            BagCode = "BAG-XY01",
            TargetChuteId = 501L
        };

        Assert.Equal(101L, args.ParcelId);
        Assert.Equal("BC-TEST-001", args.BarCodes);
        Assert.Equal("WS-A1", args.WorkstationName);
        Assert.Equal(scannedTime, args.ScannedTime);
        Assert.Equal("BAG-XY01", args.BagCode);
        Assert.Equal(501L, args.TargetChuteId);
        LocalTimeTestConstraint.AssertIsLocalTime(args.ScannedTime);
    }

    /// <summary>
    /// 验证场景：ParcelScannedEvent 是值语义（两个字段相同的实例应相等）。
    /// </summary>
    [Fact]
    public void ParcelScannedEventShouldUseValueEquality() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 20, 10, 0, 0);

        var a = new ParcelScannedEvent {
            ParcelId = 1L,
            BarCodes = "BC-001",
            WorkstationName = "WS-01",
            ScannedTime = scannedTime,
            BagCode = "BAG-01",
            TargetChuteId = 100L
        };
        var b = new ParcelScannedEvent {
            ParcelId = 1L,
            BarCodes = "BC-001",
            WorkstationName = "WS-01",
            ScannedTime = scannedTime,
            BagCode = "BAG-01",
            TargetChuteId = 100L
        };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// 验证场景：ParcelChuteAssignedEvent 各业务字段可正确构造并读取。
    /// </summary>
    [Fact]
    public void ParcelChuteAssignedEventShouldCarryAllBusinessFields() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 20, 11, 0, 0);

        var args = new ParcelChuteAssignedEvent {
            ParcelId = 202L,
            TargetChuteId = 601L,
            ActualChuteId = 602L,
            ScannedTime = scannedTime
        };

        Assert.Equal(202L, args.ParcelId);
        Assert.Equal(601L, args.TargetChuteId);
        Assert.Equal(602L, args.ActualChuteId);
        Assert.Equal(scannedTime, args.ScannedTime);
        LocalTimeTestConstraint.AssertIsLocalTime(args.ScannedTime);
    }

    /// <summary>
    /// 验证场景：ParcelChuteAssignedEvent 是值语义（两个字段相同的实例应相等）。
    /// </summary>
    [Fact]
    public void ParcelChuteAssignedEventShouldUseValueEquality() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 20, 11, 0, 0);

        var a = new ParcelChuteAssignedEvent {
            ParcelId = 1L,
            TargetChuteId = 100L,
            ActualChuteId = 101L,
            ScannedTime = scannedTime
        };
        var b = new ParcelChuteAssignedEvent {
            ParcelId = 1L,
            TargetChuteId = 100L,
            ActualChuteId = 101L,
            ScannedTime = scannedTime
        };

        Assert.Equal(a, b);
    }

    /// <summary>
    /// 验证场景：不同 TargetChuteId / ActualChuteId 的 ParcelChuteAssignedEvent 应不相等（值语义不相等场景）。
    /// </summary>
    [Fact]
    public void ParcelChuteAssignedEventWithDifferentFieldsShouldNotBeEqual() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 20, 11, 0, 0);

        var a = new ParcelChuteAssignedEvent {
            ParcelId = 1L,
            TargetChuteId = 100L,
            ActualChuteId = 101L,
            ScannedTime = scannedTime
        };
        var b = new ParcelChuteAssignedEvent {
            ParcelId = 1L,
            TargetChuteId = 100L,
            ActualChuteId = 999L,
            ScannedTime = scannedTime
        };

        Assert.NotEqual(a, b);
    }
}
