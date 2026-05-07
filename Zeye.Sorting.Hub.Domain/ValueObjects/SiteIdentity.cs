using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.ValueObjects;

/// <summary>
/// 站点标识值对象。
/// </summary>
public sealed record class SiteIdentity {
    /// <summary>
    /// 站点编码最大长度。
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// 站点编码。
    /// </summary>
    [MaxLength(MaxCodeLength)]
    public required string SiteCode { get; init; }

    /// <summary>
    /// 返回站点编码文本。
    /// </summary>
    /// <returns>站点编码。</returns>
    public override string ToString() {
        return SiteCode;
    }
}
