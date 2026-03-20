namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 通信指令记录响应合同。
/// </summary>
public sealed record CommandInfoResponse {
    /// <summary>
    /// 通信方式（枚举数值）。
    /// </summary>
    public required int ProtocolType { get; init; }

    /// <summary>
    /// 协议名称。
    /// </summary>
    public required string ProtocolName { get; init; }

    /// <summary>
    /// 连接名称。
    /// </summary>
    public required string ConnectionName { get; init; }

    /// <summary>
    /// 指令内容。
    /// </summary>
    public required string CommandPayload { get; init; }

    /// <summary>
    /// 指令产生时间。
    /// </summary>
    public required DateTime GeneratedTime { get; init; }

    /// <summary>
    /// 指令作用类型（枚举数值）。
    /// </summary>
    public required int ActionType { get; init; }

    /// <summary>
    /// 格式化说明。
    /// </summary>
    public required string FormattedMessage { get; init; }

    /// <summary>
    /// 指令方向（枚举数值）。
    /// </summary>
    public required int Direction { get; init; }
}
