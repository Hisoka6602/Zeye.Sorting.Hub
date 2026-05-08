using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.ValueObjects;

/// <summary>
/// 产线标识值对象。
/// </summary>
public sealed record class LineIdentity {
    /// <summary>
    /// 产线编码最大长度。
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// 产线编码。
    /// </summary>
    [MaxLength(MaxCodeLength)]
    public required string LineCode { get; init; }

    /// <summary>
    /// 返回产线编码文本。
    /// </summary>
    /// <returns>产线编码。</returns>
    public override string ToString() {
        return LineCode;
    }
}
