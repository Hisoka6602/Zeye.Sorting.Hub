namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>原始事实与投影结果的追溯视图。</summary>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="RecordId">事实编号。</param>
/// <param name="SourceSequence">十进制发送序号。</param>
/// <param name="BodySha256">不可变摘要。</param>
/// <param name="Kind">事实类型。</param>
/// <param name="BodyJson">保留原始内容。</param>
/// <param name="ReceivedAt">中心本地接收时间。</param>
/// <param name="ProjectionState">待处理、完成或重试状态。</param>
/// <param name="ProjectionError">脱敏投影错误。</param>
/// <param name="ParcelId">中心包裹编号。</param>
public sealed record FusionFactInspection(
    string SourceInstanceId,
    string JournalId,
    string RecordId,
    string SourceSequence,
    string BodySha256,
    string Kind,
    string BodyJson,
    DateTime ReceivedAt,
    string ProjectionState,
    string? ProjectionError,
    string? ParcelId);
