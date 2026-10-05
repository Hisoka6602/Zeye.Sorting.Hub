using System.Text.Json;

namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>保留原文的不可变来源事实。</summary>
/// <param name="SchemaVersion">结构版本。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="SourceRunId">设备计数周期。</param>
/// <param name="SourceParcelId">十进制来源包裹编号。</param>
/// <param name="RecordId">不可变事实编号。</param>
/// <param name="SourceSequence">十进制发送序号。</param>
/// <param name="Kind">事实类型。</param>
/// <param name="OccurredAtUtc">协议发生时间，仅在接入边界转换为来源本地时间。</param>
/// <param name="CapturedAtUtc">协议捕获时间。</param>
/// <param name="TimeZoneId">来源业务时区。</param>
/// <param name="ProducerSessionId">进程诊断身份。</param>
/// <param name="ProducerVersion">发送端版本。</param>
/// <param name="ConfigurationRevision">脱敏配置摘要。</param>
/// <param name="Data">来源结构化数据。</param>
public sealed record HubFactBody(
    string SchemaVersion,
    string SourceInstanceId,
    string JournalId,
    string SourceRunId,
    string? SourceParcelId,
    string RecordId,
    string SourceSequence,
    string Kind,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CapturedAtUtc,
    string TimeZoneId,
    string ProducerSessionId,
    string ProducerVersion,
    string ConfigurationRevision,
    JsonElement Data);
