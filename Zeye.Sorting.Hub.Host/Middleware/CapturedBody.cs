namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// 正文采集结果。
/// </summary>
/// <param name="Content">正文内容。</param>
/// <param name="HasBody">是否存在正文。</param>
/// <param name="IsTruncated">是否截断。</param>
/// <param name="OriginalLengthBytes">原始字节长度。</param>
internal readonly record struct CapturedBody(
    string Content,
    bool HasBody,
    bool IsTruncated,
    long OriginalLengthBytes) {
    /// <summary>
    /// 空正文采集结果。
    /// </summary>
    public static CapturedBody Empty => new(string.Empty, false, false, 0L);
}
