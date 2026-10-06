using System.Text.Json;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>不申请租约的配对身份检查请求。</summary>
public sealed record FusionConfigurationProbe(string SourceInstanceId, string HubId, string LineId, string TimeZoneId, string? SiteCode, string? DeviceCode);
