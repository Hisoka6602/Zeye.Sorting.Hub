namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>原始字符串及校验摘要。</summary>
/// <param name="RecordId">事实编号。</param>
/// <param name="SourceSequence">十进制发送序号。</param>
/// <param name="BodyJson">原始事实 JSON 字符串。</param>
/// <param name="BodySha256">原始 UTF-8 字节摘要。</param>
public sealed record HubFactEnvelope(
    string RecordId,
    string SourceSequence,
    string BodyJson,
    string BodySha256);
