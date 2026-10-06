namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>当前连接独立的来源租约缓存。</summary>
/// <param name="Source">登记来源。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="LeaseId">连接租约。</param>
/// <param name="ExpiresAt">中心本地到期时间。</param>
public sealed record FusionConnectionLease(FusionSourceOptions Source, string JournalId, string LeaseId, DateTime ExpiresAt);
