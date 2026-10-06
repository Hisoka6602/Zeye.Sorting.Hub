namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>经过认证的有界 UDP 发现报文。</summary>
/// <param name="Type">discover 或 offer。</param>
/// <param name="ProtocolVersion">协议版本。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="HubId">中心编码。</param>
/// <param name="Nonce">查询随机数。</param>
/// <param name="SentAtUnixMs">协议发送毫秒时间。</param>
/// <param name="ExpiresAtUnixMs">协议到期毫秒时间。</param>
/// <param name="Endpoint">中心服务地址。</param>
/// <param name="Signature">HMAC 摘要。</param>
public sealed record FusionDiscoveryPacket(
    string Type,
    string ProtocolVersion,
    string SourceInstanceId,
    string HubId,
    string Nonce,
    long SentAtUnixMs,
    long ExpiresAtUnixMs,
    string Endpoint,
    string Signature) {
    /// <summary>显式错误标识将被拒绝，缺省兼容经过认证的旧版报文。</summary>
    public string Protocol { get; init; } = "zeye.fusion-hub.discovery";
}
