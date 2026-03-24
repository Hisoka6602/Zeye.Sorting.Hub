namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// 响应正文采集结果。
/// </summary>
/// <param name="Content">正文内容。</param>
/// <param name="HasBody">是否存在正文。</param>
/// <param name="IsTruncated">是否截断。</param>
/// <param name="TotalBytes">响应总字节数。</param>
public readonly record struct ResponseCaptureResult(
    string Content,
    bool HasBody,
    bool IsTruncated,
    long TotalBytes);
