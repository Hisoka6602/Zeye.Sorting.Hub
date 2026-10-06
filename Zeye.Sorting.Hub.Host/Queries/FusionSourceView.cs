using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>不包含机器密钥的工作台显示信息。</summary>
public sealed record FusionSourceView(string SourceInstanceId, string WorkstationName, bool Enabled, string TenantId,
    string StoragePartitionId, string LineId, string? SiteCode, string? DeviceCode, string TimeZoneId, bool IdentityLocked);
