namespace Zeye.Sorting.Hub.Contracts.Models.Realtime;

/// <summary>实时响应保留原接口状态码与 JSON 原文，避免浏览器丢失 64 位编号。</summary>
public sealed record RealtimeResponse(int StatusCode, string Json);
