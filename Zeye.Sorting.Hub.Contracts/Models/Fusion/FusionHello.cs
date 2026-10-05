namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>工作台注册消息。</summary>
/// <param name="ProtocolVersion">协议版本。</param>
/// <param name="SourceInstanceId">稳定来源编码。</param>
/// <param name="HubId">目标中心编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="SourceRunId">设备计数周期。</param>
/// <param name="ProducerSessionId">进程诊断身份。</param>
/// <param name="ProducerVersion">发送端版本。</param>
/// <param name="TimeZoneId">来源业务时区。</param>
/// <param name="LineId">登记产线。</param>
/// <param name="SiteCode">登记站点。</param>
/// <param name="DeviceCode">登记设备。</param>
public sealed record FusionHello(
    string ProtocolVersion,
    string SourceInstanceId,
    string HubId,
    string JournalId,
    string SourceRunId,
    string ProducerSessionId,
    string ProducerVersion,
    string TimeZoneId,
    string LineId,
    string? SiteCode,
    string? DeviceCode);
