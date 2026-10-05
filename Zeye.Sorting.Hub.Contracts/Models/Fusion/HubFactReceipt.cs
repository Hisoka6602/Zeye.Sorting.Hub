namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>事务提交后的逐条确认。</summary>
/// <param name="RecordId">事实编号。</param>
/// <param name="SourceSequence">发送序号。</param>
/// <param name="BodySha256">原文摘要。</param>
/// <param name="Status">stored、duplicate、retryable、rejected 或 conflict。</param>
/// <param name="Code">安全错误编码。</param>
/// <param name="Detail">脱敏说明。</param>
public sealed record HubFactReceipt(
    string RecordId,
    string SourceSequence,
    string BodySha256,
    string Status,
    string? Code = null,
    string? Detail = null);
