using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.ValueObjects;

/// <summary>
/// 设备标识值对象。
/// </summary>
public sealed record class DeviceIdentity {
    /// <summary>
    /// 设备编码最大长度。
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// 设备编码。
    /// </summary>
    [MaxLength(MaxCodeLength)]
    public required string DeviceCode { get; init; }

    /// <summary>
    /// 返回设备编码文本。
    /// </summary>
    /// <returns>设备编码。</returns>
    public override string ToString() {
        return DeviceCode;
    }
}
