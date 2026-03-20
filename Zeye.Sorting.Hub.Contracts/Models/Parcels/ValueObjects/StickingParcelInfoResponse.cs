namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 叠包信息响应合同。
/// </summary>
public sealed record StickingParcelInfoResponse {
    /// <summary>
    /// 是否叠包。
    /// </summary>
    public required bool IsSticking { get; init; }

    /// <summary>
    /// 判断结果接收时间。
    /// </summary>
    public required DateTime? ReceiveTime { get; init; }

    /// <summary>
    /// 判断源数据内容。
    /// </summary>
    public required string RawData { get; init; }

    /// <summary>
    /// 总耗时（单位：毫秒）。
    /// </summary>
    public required int? ElapsedMilliseconds { get; init; }
}
