using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.ValueObjects;

/// <summary>
/// 运营边界值对象。
/// </summary>
public sealed record class OperationalScope {
    /// <summary>
    /// 工作站名称最大长度。
    /// </summary>
    public const int MaxWorkstationNameLength = 128;

    /// <summary>
    /// 站点标识。
    /// </summary>
    public required SiteIdentity SiteIdentity { get; init; }

    /// <summary>
    /// 产线标识。
    /// </summary>
    public LineIdentity? LineIdentity { get; init; }

    /// <summary>
    /// 设备标识。
    /// </summary>
    public DeviceIdentity? DeviceIdentity { get; init; }

    /// <summary>
    /// 工作站名称。
    /// </summary>
    [MaxLength(MaxWorkstationNameLength)]
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 站点编码。
    /// </summary>
    public string SiteCode => SiteIdentity.SiteCode;

    /// <summary>
    /// 产线编码。
    /// </summary>
    public string? LineCode => LineIdentity?.LineCode;

    /// <summary>
    /// 设备编码。
    /// </summary>
    public string? DeviceCode => DeviceIdentity?.DeviceCode;
}
