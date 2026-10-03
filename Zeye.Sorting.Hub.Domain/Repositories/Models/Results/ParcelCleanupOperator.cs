namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>已通过身份和密码校验的操作人快照；不包含密码或会话凭据。</summary>
public sealed record ParcelCleanupOperator(string UserId, string Account, string Name, string ClientIp, string TraceId);
